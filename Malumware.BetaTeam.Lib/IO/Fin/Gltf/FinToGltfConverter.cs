using System.Numerics;
using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    // Converts a FIN scene to glTF, keeping its node hierarchy so every object keeps its own local transform. Fields
    // beyond geometry go into each node's extras, which Blender shows as custom properties.
    public class FinToGltfConverter
    {
        // The tree is walked from the root, so a node listed as a child in two places is written twice. This keeps a
        // crafted file from multiplying that into more nodes than memory holds.
        public const int MAX_NODES = 100_000;
        // Shipped files nest at most 45 levels deep
        public const int MAX_DEPTH = 256;

        private const float MIN_NORMAL_LENGTH = 1e-6f;
        // Four unsigned 16-bit joint indices per vertex
        private const int JOINT_SIZE = 8;

        // By default only the most detailed level of each NiLODNode is written, and none of the flat "LOD_" stand-ins
        // that characters show from afar, so the levels don't overlap
        public bool AllLevelsOfDetail { get; }

        public FinToGltfConverter(bool allLevelsOfDetail = false)
        {
            AllLevelsOfDetail = allLevelsOfDetail;
        }

        public byte[] ToGlb(FinFile file)
        {
            return ToModel(file)
                .WriteGLB()
                .ToArray();
        }

        public void Convert(FinFile file, string outputPath)
        {
            File.WriteAllBytes(outputPath, ToGlb(file));
        }

        internal ModelRoot ToModel(FinFile file)
        {
            return new Export(file, AllLevelsOfDetail).Run();
        }

        internal static string Describe(NiObject block)
        {
            var name = block.Name is null ? "" : $" \"{block.Name}\"";
            return $"{block.ClassName}{name} at offset 0x{block.Offset:X}";
        }

        private sealed class Export
        {
            private readonly FinFile _file;
            private readonly bool _allLevelsOfDetail;
            private readonly ModelRoot _model = ModelRoot.CreateModel();
            private readonly FinGltfExtrasBuilder _extras = new();
            private readonly Dictionary<NiTriBasedGeom, Mesh?> _meshes = new(ReferenceEqualityComparer.Instance);
            private readonly HashSet<NiObject> _ancestors = new(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<NiAVObject, NiNode> _parents = new(ReferenceEqualityComparer.Instance);
            // A block listed as a child in two places gets two nodes; bones are joined to the first
            private readonly Dictionary<NiAVObject, Node> _nodes = new(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<Ni3dsSkin, FinGltfSkin> _skins = new(ReferenceEqualityComparer.Instance);
            private readonly List<(Node Node, FinGltfSkin Skin)> _skinnedNodes = [];
            private readonly HashSet<NiAVObject> _standIns = new(ReferenceEqualityComparer.Instance);
            private Material? _material;
            private int _nodeCount;

            public Export(FinFile file, bool allLevelsOfDetail)
            {
                _file = file;
                _allLevelsOfDetail = allLevelsOfDetail;
            }

            public ModelRoot Run()
            {
                var scene = _model.UseScene(_file.Name);
                var root = scene.CreateNode(_file.Name);
                root.LocalTransform = new AffineTransform(null, FinGltfTransform.ZUpToYUp, null);
                root.Extras = BuildRootExtras();

                FindParents();
                if (!_allLevelsOfDetail)
                {
                    FindStandIns();
                }
                foreach (var block in SceneRoots())
                {
                    AddNode(root, block, 0);
                }
                BindSkins();

                return _model;
            }

            private void FindParents()
            {
                foreach (var node in _file.Objects.OfType<NiNode>())
                {
                    foreach (var child in node.Children)
                    {
                        if (child.Target is not null)
                        {
                            _parents.TryAdd(child.Target, node);
                        }
                    }
                }
            }

            // Beyond a distance its shared data sets, a character hides its skinned meshes and shows flat stand-ins named
            // "LOD_..." instead; nearer, it's the other way round. The engine finds the stand-ins by walking the actor's
            // nodes, not descending into them. Like a NiLODNode's lower levels, they're only written for all levels.
            private void FindStandIns()
            {
                foreach (var actor in _file.Objects.OfType<DDActor>())
                {
                    if (actor.SharedData.Target is { LodDistanceSquared: > 0 })
                    {
                        FindStandIns(actor, 0);
                    }
                }
            }

            private void FindStandIns(NiNode node, int depth)
            {
                if (depth >= MAX_DEPTH)
                {
                    throw new InvalidDataException($"Nodes are nested more than {MAX_DEPTH} levels deep");
                }

                foreach (var child in node.Children)
                {
                    if (child.Target?.Name?.StartsWith("LOD_", StringComparison.Ordinal) == true)
                    {
                        _standIns.Add(child.Target);
                    }
                    else if (child.Target is NiNode childNode)
                    {
                        FindStandIns(childNode, depth + 1);
                    }
                }
            }

            // The file's roots, then objects no node lists as a child. Lights are the usual case: nodes list them as
            // effects, not children, and the engine places them by their own transform.
            private IEnumerable<NiAVObject> SceneRoots()
            {
                var roots = _file.TopLevelObjects
                    .OfType<NiAVObject>()
                    .ToList();
                var free = _file.Objects
                    .OfType<NiAVObject>()
                    .Where(e => !_parents.ContainsKey(e) && !roots.Contains(e));

                return roots.Concat(free);
            }

            // Blocks that aren't part of the tree and aren't written with the objects that use them, such as the
            // DDActorSharedData of a unit and texture animations. Keyed by link ID, which references in extras name.
            private JsonObject BuildRootExtras()
            {
                var blocks = new JsonObject();
                foreach (var block in _file.Objects)
                {
                    if (block is not NiAVObject && !FinGltfExtrasBuilder.IsInline(block))
                    {
                        blocks[FinGltfExtrasBuilder.FormatLinkId(block.LinkId)] = _extras.BuildBlock(block);
                    }
                }

                return new JsonObject
                {
                    ["file"] = _file.Name,
                    ["version"] = _file.Header.Version,
                    ["blocks"] = blocks,
                };
            }

            // lodLevel matches a child of a NiLODNode to its entry in the node's Ranges
            private void AddNode(Node parent, NiAVObject block, int depth, int? lodLevel = null)
            {
                if (depth >= MAX_DEPTH)
                {
                    throw new InvalidDataException($"Nodes are nested more than {MAX_DEPTH} levels deep");
                }
                if (_ancestors.Contains(block))
                {
                    throw new InvalidDataException($"{Describe(block)} is its own descendant");
                }
                if (++_nodeCount > MAX_NODES)
                {
                    throw new InvalidDataException($"The scene expands to more than {MAX_NODES} nodes");
                }

                var node = parent.CreateNode(block.Name ?? block.ClassName);
                node.LocalTransform = FinGltfTransform.ToLocalTransform(block);
                _nodes.TryAdd(block, node);
                var extras = _extras.BuildNode(block);
                if (lodLevel is not null)
                {
                    extras["lodLevel"] = lodLevel;
                }

                if (block is NiTriBasedGeom geometry)
                {
                    node.Mesh = GetMesh(geometry);
                    if (node.Mesh is not null && GetSkin(geometry) is { } skin)
                    {
                        _skinnedNodes.Add((node, skin));
                    }
                    AddTextureW(extras, geometry);
                    AddNonFiniteTextureCount(extras, geometry);
                }

                if (block is NiNode parentBlock)
                {
                    _ancestors.Add(block);
                    foreach (var (index, child) in ExportedChildren(parentBlock))
                    {
                        AddNode(node, child, depth + 1, parentBlock is NiLODNode ? index : null);
                    }
                    _ancestors.Remove(block);
                }

                node.Extras = extras;
            }

            // Runs once every node exists, because a skin can come before its bones in the tree. Binding at the current
            // (rest) transforms makes the baked vertices the bind pose, so the mesh looks the same until a bone moves.
            private void BindSkins()
            {
                foreach (var (node, skin) in _skinnedNodes)
                {
                    var joints = new Node[skin.Bones.Count];
                    for (var i = 0; i < joints.Length; i++)
                    {
                        if (!_nodes.TryGetValue(skin.Bones[i], out var joint))
                        {
                            throw new InvalidDataException(
                                $"{Describe(skin.Bones[i])} moves a skinned mesh but isn't part of the exported scene"
                            );
                        }
                        joints[i] = joint;
                    }

                    node.Skin = _model.CreateSkin(node.Name);
                    node.Skin.BindJoints(node.WorldMatrix, joints);
                }
            }

            private IEnumerable<(int Index, NiAVObject Child)> ExportedChildren(NiNode block)
            {
                var children = block.Children
                    .Select((child, index) => (Index: index, Child: child.Target))
                    .Where(e => e.Child is not null && !_standIns.Contains(e.Child))
                    .Select(e => (e.Index, e.Child!));

                if (block is not NiLODNode lod || _allLevelsOfDetail)
                {
                    return children;
                }

                var level = MostDetailedLevel(lod);

                return level is null ? children : children.Where(e => e.Index == level);
            }

            // The level shown closest to the camera. Without usable ranges there's no telling, so every level is kept.
            private static int? MostDetailedLevel(NiLODNode lod)
            {
                int? best = null;
                var count = Math.Min(lod.Ranges.Length, lod.Children.Count);
                for (var i = 0; i < count; i++)
                {
                    if (lod.Children[i].Target is not null && (best is null || lod.Ranges[i].Near < lod.Ranges[best.Value].Near))
                    {
                        best = i;
                    }
                }

                return best;
            }

            private Mesh? GetMesh(NiTriBasedGeom geometry)
            {
                if (!_meshes.TryGetValue(geometry, out var mesh))
                {
                    mesh = CreateMesh(geometry);
                    _meshes[geometry] = mesh;
                }

                return mesh;
            }

            private FinGltfSkin? GetSkin(NiTriBasedGeom geometry)
            {
                if (geometry is not Ni3dsSkin { SkinVertices: not null } block)
                {
                    return null;
                }
                if (!_skins.TryGetValue(block, out var skin))
                {
                    skin = FinGltfSkin.Create(block, _parents);
                    _skins[block] = skin;
                }

                return skin;
            }

            private Mesh? CreateMesh(NiTriBasedGeom geometry)
            {
                if (geometry.Vertices is not { Length: > 0 })
                {
                    return null;
                }

                var skin = GetSkin(geometry);
                var vertices = skin?.Vertices ?? geometry.Vertices;
                var mesh = _model.CreateMesh(geometry.Name ?? geometry.ClassName);
                var primitive = mesh
                    .CreatePrimitive()
                    .WithVertexAccessor("POSITION", RequireFinite(geometry, "vertex", vertices));

                if (NormalizeNormals(skin?.Normals ?? geometry.Normals) is { } normals)
                {
                    primitive.WithVertexAccessor("NORMAL", normals);
                }

                if (skin is not null)
                {
                    primitive.SetVertexAccessor("JOINTS_0", CreateJointAccessor(skin.Joints));
                    primitive.WithVertexAccessor("WEIGHTS_0", skin.Weights);
                }

                if (geometry.Colors is { } colors)
                {
                    var values = colors
                        .Select(e => new Vector4(e.R, e.G, e.B, e.A))
                        .ToArray();
                    primitive.WithVertexAccessor("COLOR_0", RequireFinite(geometry, "colour", values));
                }

                // glTF coordinates have two components; the unexplained third one goes into the extras
                for (var set = 0; set < (geometry.TextureSets?.Count ?? 0); set++)
                {
                    var coordinates = geometry.TextureSets![set]
                        .Select(e => new Vector2(FiniteOrZero(e.X), FiniteOrZero(e.Y)))
                        .ToArray();
                    primitive.WithVertexAccessor($"TEXCOORD_{set}", coordinates);
                }

                if (geometry is NiTriShape { Triangles.Length: > 0 } shape)
                {
                    primitive
                        .WithIndicesAccessor(PrimitiveType.TRIANGLES, TriangleIndices(shape))
                        .WithMaterial(GetMaterial());
                }
                else
                {
                    // No triangles (a DDCorona, whose quad the engine builds when drawing): keep the vertices as points
                    primitive.WithIndicesAutomatic(PrimitiveType.POINTS);
                }

                return mesh;
            }

            // glTF requires joint indices as unsigned integers; WithVertexAccessor would store these vectors as floats
            private Accessor CreateJointAccessor(Vector4[] joints)
            {
                var size = joints.Length * JOINT_SIZE;
                var view = _model.UseBufferView(new byte[size], 0, size, JOINT_SIZE, BufferMode.ARRAY_BUFFER);
                var accessor = _model.CreateAccessor("JOINTS_0");
                accessor.SetVertexData(view, 0, joints.Length, new AttributeFormat(DimensionType.VEC4, EncodingType.UNSIGNED_SHORT));
                var values = accessor.AsVector4Array();
                for (var i = 0; i < joints.Length; i++)
                {
                    values[i] = joints[i];
                }

                return accessor;
            }

            private static int[] TriangleIndices(NiTriShape shape)
            {
                var indices = new int[shape.Triangles.Length * 3];
                for (var i = 0; i < shape.Triangles.Length; i++)
                {
                    var triangle = shape.Triangles[i];
                    if (triangle.A >= shape.VertexCount || triangle.B >= shape.VertexCount || triangle.C >= shape.VertexCount)
                    {
                        throw new InvalidDataException(
                            $"{Describe(shape)} has a triangle past its {shape.VertexCount} vertices"
                        );
                    }
                    indices[i * 3] = triangle.A;
                    indices[i * 3 + 1] = triangle.B;
                    indices[i * 3 + 2] = triangle.C;
                }

                return indices;
            }

            // glTF requires unit normals. The direction is what counts for shading, so scaling them loses nothing; a
            // zero or broken normal has no direction to keep, and then Blender computes its own for the whole mesh.
            private static Vector3[]? NormalizeNormals(Vector3[]? normals)
            {
                if (normals is null)
                {
                    return null;
                }

                var result = new Vector3[normals.Length];
                for (var i = 0; i < normals.Length; i++)
                {
                    var length = normals[i].Length();
                    if (!float.IsFinite(length) || length < MIN_NORMAL_LENGTH)
                    {
                        return null;
                    }
                    result[i] = normals[i] / length;
                }

                return result;
            }

            private static void AddTextureW(JsonObject extras, NiTriBasedGeom geometry)
            {
                if (geometry.TextureSets is not { } sets || sets.All(set => set.All(e => e.Z == 0)))
                {
                    return;
                }

                var array = new JsonArray();
                foreach (var set in sets)
                {
                    var values = set
                        .Select(e => (JsonNode?)FinGltfExtrasBuilder.BuildFloat(e.Z))
                        .ToArray();
                    array.Add(new JsonArray(values));
                }
                extras["TextureCoordinateW"] = array;
            }

            // Some shipped files have NaN or infinite u or v, probably from a mapping that divided by zero in the
            // exporter. glTF doesn't allow them, so we deliberately write 0 for each such component and count the
            // coordinates in the extras. Nearly all are in the second texture set of shapes with one or two triangles,
            // which Blender's default material doesn't use; once textures are converted, an affected triangle shows a
            // single texel instead of whatever the game's renderer made of NaN. Only u and v are checked: w goes into
            // the extras, which can hold any value.
            private static float FiniteOrZero(float value)
            {
                return float.IsFinite(value) ? value : 0;
            }

            private static void AddNonFiniteTextureCount(JsonObject extras, NiTriBasedGeom geometry)
            {
                var count = geometry.TextureSets?
                    .Sum(set => set.Count(e => !float.IsFinite(e.X) || !float.IsFinite(e.Y))) ?? 0;
                if (count > 0)
                {
                    extras["NonFiniteTextureCoordinates"] = count;
                }
            }

            // Textures and materials aren't converted yet, so every shape gets the same plain white, non-metallic one.
            // Vertex colours still show, because glTF multiplies them with the base colour.
            private Material GetMaterial()
            {
                return _material ??= _model
                    .CreateMaterial("Default")
                    .WithPBRMetallicRoughness(Vector4.One, null, null, 0, 1);
            }

            private static T[] RequireFinite<T>(NiObject block, string what, T[] values)
                where T : struct
            {
                foreach (var value in values)
                {
                    var finite = value switch
                    {
                        Vector3 v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z),
                        Vector4 v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z) && float.IsFinite(v.W),
                        _ => true,
                    };
                    if (!finite)
                    {
                        throw new InvalidDataException($"{Describe(block)} has a {what} that isn't a finite number");
                    }
                }

                return values;
            }
        }
    }
}
