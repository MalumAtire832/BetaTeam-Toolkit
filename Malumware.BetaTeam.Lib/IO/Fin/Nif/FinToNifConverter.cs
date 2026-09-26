using System.Globalization;
using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes;
using Malumware.BetaTeam.Lib.IO.Nif;
using Malumware.BetaTeam.Lib.IO.Nif.Blocks.BoundingVolumes;
using FinBlocks = Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using NifBlocks = Malumware.BetaTeam.Lib.IO.Nif.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin.Nif
{
    // Converts a FIN scene graph to NIF 4.0.0.2 for viewing in NifSkope. What NIF has no field for, such as the
    // Digital Domain game object data, is kept as string extra data on the object it belongs to. Animation and
    // lights aren't exported; the warnings list what was left out.
    public class FinToNifConverter
    {
        // Same limit as the dump: deeper than any shipped file, shallow enough to keep a crafted file from
        // overflowing the stack
        public const int MAX_DEPTH = 256;

        public FinNifConversion Convert(FinFile file)
        {
            return new Conversion().Run(file);
        }

        private sealed class Conversion
        {
            // Texture slots for the stages of a NiMultiTextureProperty, in stage order. The bump map slot needs
            // settings FIN doesn't have, so it is skipped.
            private static readonly NifBlocks.NifTextureSlot[] STAGE_SLOTS =
            [
                NifBlocks.NifTextureSlot.Base,
                NifBlocks.NifTextureSlot.Dark,
                NifBlocks.NifTextureSlot.Detail,
                NifBlocks.NifTextureSlot.Gloss,
                NifBlocks.NifTextureSlot.Glow,
                NifBlocks.NifTextureSlot.Decal0,
            ];

            private readonly FinNifWarnings _warnings = new();
            // Blocks that were converted or already warned about; the rest are reported at the end
            private readonly HashSet<FinBlocks.NiObject> _handled = new(ReferenceEqualityComparer.Instance);
            private readonly HashSet<FinBlocks.NiObject> _inProgress = new(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<FinBlocks.NiAVObject, NifBlocks.NiAVObject?> _objects = new(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<FinBlocks.NiProperty, NifBlocks.NiProperty?> _properties = new(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<FinBlocks.NiImage, NifBlocks.NiSourceTexture> _images = new(ReferenceEqualityComparer.Instance);
            private readonly Dictionary<FinNifTextureState, NifBlocks.NiTexturingProperty> _texturing = [];

            public FinNifConversion Run(FinFile file)
            {
                var roots = new List<NifBlocks.NiObject>();
                foreach (var topLevelObject in file.TopLevelObjects)
                {
                    if (topLevelObject is not FinBlocks.NiAVObject source)
                    {
                        _warnings.Add($"Top-level {topLevelObject.ClassName} isn't a scene object and isn't exported");
                        _handled.Add(topLevelObject);
                        continue;
                    }

                    var root = ConvertObject(source, default, 0);
                    if (root is not null)
                    {
                        roots.Add(root);
                    }
                }

                foreach (var block in file.Objects)
                {
                    if (!_handled.Contains(block))
                    {
                        _warnings.Add($"{block.ClassName} isn't exported: {DescribeUnexported(block)}");
                    }
                }

                return new FinNifConversion(new NifFile(roots), _warnings.ToList());
            }

            private static string DescribeUnexported(FinBlocks.NiObject block)
            {
                return block switch
                {
                    FinBlocks.NiFlipTextures => "texture animation isn't supported yet",
                    FinBlocks.Ni3dsLightAnimator => "colour and transparency animation isn't supported yet",
                    FinBlocks.NiLight => "light types aren't mapped yet",
                    _ => "not reachable from a top-level object",
                };
            }

            private NifBlocks.NiAVObject? ConvertObject(FinBlocks.NiAVObject source, FinNifTextureState inherited, int depth)
            {
                if (depth > MAX_DEPTH)
                {
                    throw new InvalidDataException(
                        $"Scene graph nested more than {MAX_DEPTH} deep at {source.ClassName} at offset 0x{source.Offset:X}"
                    );
                }

                if (_inProgress.Contains(source))
                {
                    _warnings.Add($"{source.ClassName} at offset 0x{source.Offset:X} is its own ancestor; the loop is cut");
                    return null;
                }

                // A scene graph is a tree, so each object converts once. Should one have two parents, both link
                // the same NIF block, converted with the textures in effect at the first.
                if (_objects.TryGetValue(source, out var existing))
                {
                    return existing;
                }

                var target = CreateObject(source);
                _objects[source] = target;
                if (target is null)
                {
                    return null;
                }

                _handled.Add(source);
                _inProgress.Add(source);

                CopyObject(source, target);
                var textures = ConvertProperties(source, target, inherited);

                switch (source)
                {
                    case FinBlocks.NiNode node:
                        ConvertNode(node, (NifBlocks.NiNode)target, textures, depth);
                        break;
                    case FinBlocks.NiTriBasedGeom geometry:
                        ConvertGeometry(geometry, (NifBlocks.NiTriShape)target);
                        break;
                }

                _inProgress.Remove(source);
                return target;
            }

            private NifBlocks.NiAVObject? CreateObject(FinBlocks.NiAVObject source)
            {
                switch (source)
                {
                    case FinBlocks.NiLight:
                        // Not exported, and reported with the other unexported blocks
                        return null;
                    case FinBlocks.NiLODNode:
                        return new NifBlocks.NiLODNode();
                    case FinBlocks.NiBillboardNode:
                        return new NifBlocks.NiBillboardNode();
                    case FinBlocks.NiNode:
                        return new NifBlocks.NiNode();
                    case FinBlocks.NiTriBasedGeom:
                        return new NifBlocks.NiTriShape();
                    default:
                        _warnings.Add($"{source.ClassName} has no NIF equivalent and isn't exported");
                        _handled.Add(source);
                        return null;
                }
            }

            private void CopyObject(FinBlocks.NiAVObject source, NifBlocks.NiAVObject target)
            {
                target.Name = source.Name ?? "";
                if (source.ClassName != target.ClassName)
                {
                    target.AddExtraData(new NifBlocks.NiStringExtraData($"FinClass: {source.ClassName}"));
                }

                target.Flags = source.AppCulled ? NifBlocks.NiAVObject.FLAG_HIDDEN : (ushort)0;
                target.Translation = source.Translation;
                target.Rotation = ConvertMatrix(source.Rotation);
                target.Scale = source.Scale;
                target.Velocity = source.Velocity;
                CheckExtraData(source);

                if (source.BoundingVolume is not null)
                {
                    target.BoundingVolume = ConvertBoundingVolume(source.BoundingVolume);
                    if (target.BoundingVolume is null)
                    {
                        target.AddExtraData(new NifBlocks.NiStringExtraData($"BoundingVolume: {Describe(source.BoundingVolume)}"));
                        _warnings.Add(
                            "Collision shapes with capsules, lozenges, half spaces, intersections or inverted shapes " +
                            "have no NIF equivalent; they are described in string extra data"
                        );
                    }
                }
            }

            // Copied in file order, which assumes both engines save NiMatrix3 the same way (inferred)
            private static NifMatrix33 ConvertMatrix(FinMatrix3 m)
            {
                return new NifMatrix33(m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33);
            }

            private void CheckExtraData(FinBlocks.NiObject source)
            {
                foreach (var extraData in source.ExtraData)
                {
                    // The links to animation blocks go with the animation, which isn't exported
                    if (extraData is not FinBlocks.TexturePropExtraData and not FinBlocks.Ni3dsPropAnimExtraData)
                    {
                        _warnings.Add($"{extraData.ClassName} extra data isn't exported");
                    }
                }
            }

            private void ConvertNode(FinBlocks.NiNode source, NifBlocks.NiNode target, FinNifTextureState textures, int depth)
            {
                switch (source)
                {
                    case FinBlocks.DDActor actor:
                        ConvertActor(actor, target);
                        break;
                    case FinBlocks.Ni3dsAnimationNode:
                        _warnings.Add($"{source.ClassName} keyframes aren't exported; the node keeps its stored transform");
                        break;
                }

                switch (target)
                {
                    case NifBlocks.NiLODNode lod:
                        ConvertLod((FinBlocks.NiLODNode)source, lod);
                        break;
                    case NifBlocks.NiBillboardNode:
                        ConvertBillboard((FinBlocks.NiBillboardNode)source, target);
                        break;
                }

                if (source.Effects.Count > 0)
                {
                    _warnings.Add("Lights aren't exported, so nodes lose their light lists");
                }

                // Empty and unexported children keep their slot, so a LOD node's ranges still match its children
                foreach (var child in source.Children)
                {
                    target.Children.Add(child.Target is null ? null : ConvertObject(child.Target, textures, depth + 1));
                }
            }

            private void ConvertLod(FinBlocks.NiLODNode source, NifBlocks.NiLODNode target)
            {
                target.Index = (uint)Math.Max(0, source.ActiveChildIndex);
                foreach (var range in source.Ranges)
                {
                    target.LodLevels.Add(new NifLodRange(range.Near, range.Far));
                }

                // FIN stores a center per range, NIF one for the node
                if (source.Ranges.Length == 0)
                {
                    return;
                }
                target.LodCenter = source.Ranges[0].Center;
                if (source.Ranges.Any(range => range.Center != target.LodCenter))
                {
                    var centers = string.Join(", ", source.Ranges.Select(range => Format(range.Center)));
                    target.AddExtraData(new NifBlocks.NiStringExtraData($"LodCenters: {centers}"));
                    _warnings.Add("LOD ranges with different centers use the first; all are listed in string extra data");
                }
            }

            // FIN's modes 0 to 2 are taken to be NetImmerse's billboard modes, which NIF numbers the same way
            private void ConvertBillboard(FinBlocks.NiBillboardNode source, NifBlocks.NiNode target)
            {
                if (source.Mode is >= 0 and <= 2)
                {
                    target.Flags |= (ushort)(source.Mode << NifBlocks.NiBillboardNode.MODE_SHIFT);
                    return;
                }
                target.AddExtraData(new NifBlocks.NiStringExtraData($"BillboardMode: {source.Mode}"));
                _warnings.Add($"Billboard mode {source.Mode} has no NIF equivalent; the node always faces the camera");
            }

            private void ConvertActor(FinBlocks.DDActor actor, NifBlocks.NiNode target)
            {
                var shared = actor.SharedData.Target;
                if (shared is null)
                {
                    return;
                }

                _handled.Add(shared);
                CheckExtraData(shared);
                AddString(target, "Type", shared.Name);
                AddString(target, "Description", shared.Description);
                AddString(target, "Behavior", shared.BehaviorName);
                AddString(target, "MakeShadow", shared.MakeShadow ? "true" : "false");
                AddString(target, "Prop", shared.IsProp ? "true" : "false");
                AddString(target, "ShadowMultiplier", Format(shared.ShadowMultiplier));
                if (shared.FloorPoints.Length > 0)
                {
                    AddString(target, "FloorPoints", string.Join(", ", shared.FloorPoints.Select(Format)));
                }

                if (shared.Skills.Count > 0)
                {
                    // The skills' times are copied as stored, in the timeline's own units
                    var keys = new NifBlocks.NiTextKeyExtraData();
                    var skillKeys = shared.Skills
                        .SelectMany(skill => new[]
                        {
                            new NifBlocks.NifTextKey(skill.StartTime, $"{skill.Name}: start"),
                            new NifBlocks.NifTextKey(skill.EndTime, $"{skill.Name}: stop"),
                        })
                        .OrderBy(key => key.Time);
                    keys.TextKeys.AddRange(skillKeys);
                    target.AddExtraData(keys);
                }

                foreach (var skill in shared.Skills)
                {
                    if (skill.Sound?.FileName is { } fileName)
                    {
                        var at = skill.Sound.NodeName is null ? "" : $" at {skill.Sound.NodeName}";
                        AddString(target, "SkillSound", $"{skill.Name}: {fileName}{at}");
                    }
                }

                if (shared.Tracks.Count > 0)
                {
                    _warnings.Add("DDActorSharedData keyframe tracks aren't exported");
                }
            }

            private static void AddString(NifBlocks.NiObjectNET target, string key, string? value)
            {
                if (value is not null)
                {
                    target.AddExtraData(new NifBlocks.NiStringExtraData($"{key}: {value}"));
                }
            }

            private void ConvertGeometry(FinBlocks.NiTriBasedGeom source, NifBlocks.NiTriShape target)
            {
                switch (source)
                {
                    case FinBlocks.Ni3dsSkin:
                        _warnings.Add("Ni3dsSkin bone weights aren't exported; the mesh keeps its stored pose");
                        break;
                    case FinBlocks.Ni3dsMorphShape:
                        _warnings.Add("Ni3dsMorphShape morph targets aren't exported; the mesh keeps its base vertices");
                        break;
                    case FinBlocks.DDCorona corona:
                        AddString(target, "CoronaSize", Format(corona.Size));
                        break;
                }

                var data = new NifBlocks.NiTriShapeData
                {
                    VertexCount = source.VertexCount,
                    Vertices = source.Vertices,
                    Normals = source.Normals,
                    BoundCenter = source.BoundCenter,
                    BoundRadius = source.BoundRadius,
                    VertexColors = source.Colors?.Select(c => new NifColor4(c.R, c.G, c.B, c.A)).ToArray(),
                    // A corona has no triangles of its own: the engine builds them when drawing
                    Triangles = source is FinBlocks.NiTriShape shape
                        ? shape.Triangles.Select(t => new NifTriangle(t.A, t.B, t.C)).ToArray()
                        : [],
                };

                // NIF coordinates have two components; FIN's third isn't understood yet
                var textureSets = source.TextureSets ?? [];
                foreach (var textureSet in textureSets.Take(NifBlocks.NiGeometryData.MAX_UV_SETS))
                {
                    data.UvSets.Add(textureSet.Select(uv => new Vector2(uv.X, uv.Y)).ToArray());
                }
                if (textureSets.Count > NifBlocks.NiGeometryData.MAX_UV_SETS)
                {
                    _warnings.Add($"Only the first {NifBlocks.NiGeometryData.MAX_UV_SETS} texture coordinate sets fit in NIF");
                }

                target.Data = data;
            }

            private FinNifTextureState ConvertProperties(FinBlocks.NiAVObject source, NifBlocks.NiAVObject target, FinNifTextureState inherited)
            {
                var textures = inherited;
                foreach (var reference in source.Properties)
                {
                    switch (reference.Target)
                    {
                        case null:
                            break;
                        case FinBlocks.NiTextureProperty texture:
                            textures = textures with { Texture = texture };
                            _handled.Add(texture);
                            break;
                        case FinBlocks.NiTextureModeProperty mode:
                            textures = textures with { Mode = mode };
                            _handled.Add(mode);
                            break;
                        case FinBlocks.NiMultiTextureProperty multiTexture:
                            textures = textures with { MultiTexture = multiTexture };
                            _handled.Add(multiTexture);
                            break;
                        case var property:
                            var converted = ConvertProperty(property);
                            if (converted is not null)
                            {
                                target.Properties.Add(converted);
                            }
                            break;
                    }
                }

                // A change to any of the texture properties needs a new NiTexturingProperty that combines them
                if (textures != inherited && textures.HasImages)
                {
                    target.Properties.Add(GetTexturingProperty(textures));
                }
                return textures;
            }

            private NifBlocks.NiProperty? ConvertProperty(FinBlocks.NiProperty source)
            {
                if (_properties.TryGetValue(source, out var existing))
                {
                    return existing;
                }

                NifBlocks.NiProperty? target = source switch
                {
                    FinBlocks.NiMaterialProperty material => new NifBlocks.NiMaterialProperty
                    {
                        AmbientColor = ConvertColor(material.AmbientColor),
                        DiffuseColor = ConvertColor(material.DiffuseColor),
                        SpecularColor = ConvertColor(material.SpecularColor),
                        EmissiveColor = ConvertColor(material.Emittance),
                        Glossiness = material.Shininess,
                        Alpha = material.Alpha,
                    },
                    FinBlocks.NiAlphaProperty alpha => ConvertAlpha(alpha),
                    FinBlocks.NiVertexColorProperty vertexColor => ConvertVertexColor(vertexColor),
                    FinBlocks.NiZBufferProperty zBuffer => new NifBlocks.NiZBufferProperty
                    {
                        Flags = (ushort)((zBuffer.ZBufferTest ? NifBlocks.NiZBufferProperty.FLAG_TEST : 0)
                            | (zBuffer.ZBufferWrite ? NifBlocks.NiZBufferProperty.FLAG_WRITE : 0)),
                    },
                    FinBlocks.NiSpecularProperty specular => new NifBlocks.NiSpecularProperty { Enabled = specular.Specular },
                    FinBlocks.NiShadeProperty shade => new NifBlocks.NiShadeProperty { Smooth = shade.Smooth },
                    _ => null,
                };

                _properties[source] = target;
                _handled.Add(source);
                if (target is null)
                {
                    _warnings.Add($"{source.ClassName} has no NIF equivalent and isn't exported");
                    return null;
                }

                target.Name = source.Name ?? "";
                CheckExtraData(source);
                return target;
            }

            private static NifColor3 ConvertColor(FinColor3 color)
            {
                return new NifColor3(color.R, color.G, color.B);
            }

            // FIN's blend modes are taken to be NetImmerse's AlphaFunction values, which NIF uses too
            private NifBlocks.NiAlphaProperty ConvertAlpha(FinBlocks.NiAlphaProperty source)
            {
                var target = new NifBlocks.NiAlphaProperty();
                var sourceBlend = source.SourceBlendMode;
                var destinationBlend = source.DestinationBlendMode;
                if (sourceBlend > NifBlocks.NiAlphaProperty.MAX_BLEND_FUNCTION
                    || destinationBlend > NifBlocks.NiAlphaProperty.MAX_BLEND_FUNCTION)
                {
                    target.AddExtraData(new NifBlocks.NiStringExtraData($"BlendModes: {sourceBlend}, {destinationBlend}"));
                    _warnings.Add("Alpha blend modes above 15 don't fit NIF's flags; NIF's defaults are used");
                    sourceBlend = NifBlocks.NiAlphaProperty.BLEND_SOURCE_ALPHA;
                    destinationBlend = NifBlocks.NiAlphaProperty.BLEND_INVERSE_SOURCE_ALPHA;
                }

                var blend = source.AlphaBlending ? NifBlocks.NiAlphaProperty.FLAG_BLEND : 0u;
                target.Flags = (ushort)(blend
                    | (sourceBlend << NifBlocks.NiAlphaProperty.SOURCE_BLEND_SHIFT)
                    | (destinationBlend << NifBlocks.NiAlphaProperty.DESTINATION_BLEND_SHIFT));
                return target;
            }

            // FIN has one colour mode where NIF has a vertex mode and a lighting mode. Which FIN value means what
            // isn't known, so NIF's defaults are used and the FIN value is kept.
            private static NifBlocks.NiVertexColorProperty ConvertVertexColor(FinBlocks.NiVertexColorProperty source)
            {
                var target = new NifBlocks.NiVertexColorProperty();
                target.AddExtraData(new NifBlocks.NiStringExtraData($"ColorMode: {source.ColorMode}"));
                return target;
            }

            private NifBlocks.NiTexturingProperty GetTexturingProperty(FinNifTextureState state)
            {
                if (_texturing.TryGetValue(state, out var existing))
                {
                    return existing;
                }

                var target = new NifBlocks.NiTexturingProperty();
                _texturing[state] = target;

                var nextSlot = 0;
                if (state.Texture is { } texture)
                {
                    target.Name = texture.Name ?? "";
                    CheckExtraData(texture);
                    var images = texture.Images.Select(image => image.Target).ToList();
                    target.Textures[(int)NifBlocks.NifTextureSlot.Base] = new NifBlocks.NifTexDesc
                    {
                        Source = ConvertImage(PickImage(texture, images)),
                        ClampMode = CheckClampMode(state.Mode?.ClampMode),
                        FilterMode = CheckFilterMode(state.Mode?.FilterMode),
                    };
                    nextSlot = 1;

                    // The other images are frames of a texture animation, which isn't exported
                    if (images.Count > 1)
                    {
                        var names = string.Join(", ", images.Select(image => image?.FileName ?? "(none)"));
                        target.AddExtraData(new NifBlocks.NiStringExtraData($"Images: {names}"));
                    }
                }

                if (state.Mode is { } mode)
                {
                    CheckExtraData(mode);
                    if (mode.ApplyMode <= NifBlocks.NiTexturingProperty.MAX_APPLY_MODE)
                    {
                        target.ApplyMode = mode.ApplyMode;
                    }
                    else
                    {
                        target.AddExtraData(new NifBlocks.NiStringExtraData($"ApplyMode: {mode.ApplyMode}"));
                        _warnings.Add($"Texture apply mode {mode.ApplyMode} has no NIF equivalent; modulate is used");
                    }
                }

                if (state.MultiTexture is { } multiTexture)
                {
                    AddStages(multiTexture, target, nextSlot);
                }
                return target;
            }

            private void AddStages(FinBlocks.NiMultiTextureProperty source, NifBlocks.NiTexturingProperty target, int firstSlot)
            {
                CheckExtraData(source);
                if (target.Name.Length == 0)
                {
                    target.Name = source.Name ?? "";
                }

                var available = STAGE_SLOTS.Length - firstSlot;
                if (source.Images.Count > available)
                {
                    _warnings.Add($"Only {available} multi-texture stages fit in the free NIF texture slots");
                }

                for (var stage = 0; stage < Math.Min(source.Images.Count, available); stage++)
                {
                    target.Textures[(int)STAGE_SLOTS[firstSlot + stage]] = new NifBlocks.NifTexDesc
                    {
                        Source = ConvertImage(source.Images[stage].Target),
                        ClampMode = CheckClampMode(ValueAt(source.ClampModes, stage)),
                        FilterMode = CheckFilterMode(ValueAt(source.FilterModes, stage)),
                        // Taken to be the stage's own coordinate set
                        UvSet = (uint)stage,
                    };
                }

                // How each stage combines with the one before; NIF's slots each combine in their own fixed way
                if (source.CombineModes.Length > 0)
                {
                    target.AddExtraData(new NifBlocks.NiStringExtraData($"CombineModes: {string.Join(", ", source.CombineModes)}"));
                }
            }

            private static uint? ValueAt(uint[] values, int index)
            {
                return index < values.Length ? values[index] : null;
            }

            private FinBlocks.NiImage? PickImage(FinBlocks.NiTextureProperty texture, List<FinBlocks.NiImage?> images)
            {
                if (texture.Index >= 0 && texture.Index < images.Count)
                {
                    return images[texture.Index];
                }

                if (images.Count > 0)
                {
                    _warnings.Add($"Texture index {texture.Index} is outside its {images.Count} images; the first is used");
                    return images[0];
                }
                return null;
            }

            // FIN's clamp and filter modes are taken to be NetImmerse's, which NIF numbers the same way
            private uint CheckClampMode(uint? mode)
            {
                if (mode is null)
                {
                    return NifBlocks.NifTexDesc.CLAMP_WRAP_S_WRAP_T;
                }
                if (mode > NifBlocks.NifTexDesc.MAX_CLAMP_MODE)
                {
                    _warnings.Add($"Texture clamp mode {mode} has no NIF equivalent; wrapping is used");
                    return NifBlocks.NifTexDesc.CLAMP_WRAP_S_WRAP_T;
                }
                return mode.Value;
            }

            private uint CheckFilterMode(uint? mode)
            {
                if (mode is null)
                {
                    return NifBlocks.NifTexDesc.FILTER_TRILERP;
                }
                if (mode > NifBlocks.NifTexDesc.MAX_FILTER_MODE)
                {
                    _warnings.Add($"Texture filter mode {mode} has no NIF equivalent; trilinear is used");
                    return NifBlocks.NifTexDesc.FILTER_TRILERP;
                }
                return mode.Value;
            }

            private NifBlocks.NiSourceTexture? ConvertImage(FinBlocks.NiImage? image)
            {
                if (image is null)
                {
                    return null;
                }
                if (_images.TryGetValue(image, out var existing))
                {
                    return existing;
                }

                _handled.Add(image);
                CheckExtraData(image);
                var target = new NifBlocks.NiSourceTexture { Name = image.Name ?? "" };
                if (image.External)
                {
                    target.FileName = image.FileName ?? "";
                }
                else
                {
                    _warnings.Add("Images stored inside the file aren't exported");
                }

                _images[image] = target;
                return target;
            }

            // Spheres, boxes and unions of them are the shapes NIF and FIN store the same way. NIF has no inverted
            // shapes, and its capsule is stored differently.
            private static NifBoundingVolume? ConvertBoundingVolume(NiBoundingVolume volume)
            {
                switch (volume)
                {
                    case NiSphereBV { Inverted: false } sphere:
                        return new NifSphereBV { Center = sphere.Center, Radius = sphere.Radius };
                    case NiBoxBV { Inverted: false } box:
                        return new NifBoxBV { Center = box.Center, Axes = box.Axes, Extents = box.Extents };
                    case NiUnionBV union:
                        var target = new NifUnionBV();
                        foreach (var child in union.Volumes)
                        {
                            var converted = ConvertBoundingVolume(child);
                            if (converted is null)
                            {
                                return null;
                            }
                            target.Volumes.Add(converted);
                        }
                        return target;
                    default:
                        return null;
                }
            }

            private static string Describe(NiBoundingVolume volume)
            {
                return volume switch
                {
                    NiSphereBV sphere => $"sphere(center {Format(sphere.Center)}, radius {Format(sphere.Radius)}{Inverted(sphere.Inverted)})",
                    NiBoxBV box => $"box(center {Format(box.Center)}, axes {string.Join(" ", box.Axes.Select(Format))}, extents {Format(box.Extents)}{Inverted(box.Inverted)})",
                    NiCapsuleBV capsule => $"capsule(origin {Format(capsule.Origin)}, direction {Format(capsule.Direction)}, radius {Format(capsule.Radius)}{Inverted(capsule.Inverted)})",
                    NiLozengeBV lozenge => $"lozenge(origin {Format(lozenge.Origin)}, edges {Format(lozenge.Edge0)} {Format(lozenge.Edge1)}, radius {Format(lozenge.Radius)})",
                    NiUnionBV union => $"union({string.Join(", ", union.Volumes.Select(Describe))})",
                    DDHalfSpaceBV halfSpace => $"halfspace(normal {Format(halfSpace.Plane.Normal)}, constant {Format(halfSpace.Plane.Constant)})",
                    DDIntersectionBV intersection => $"intersection({string.Join(", ", intersection.Volumes.Select(Describe))})",
                    _ => volume.GetType().Name,
                };
            }

            private static string Inverted(bool inverted)
            {
                return inverted ? ", inverted" : "";
            }

            private static string Format(float value)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }

            private static string Format(Vector3 value)
            {
                return $"({Format(value.X)} {Format(value.Y)} {Format(value.Z)})";
            }
        }
    }
}
