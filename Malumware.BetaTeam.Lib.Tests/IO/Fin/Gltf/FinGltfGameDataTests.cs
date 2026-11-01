using System.Numerics;
using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;
using Malumware.BetaTeam.Lib.IO.Pac;
using Malumware.BetaTeam.Lib.Tests.GameData;
using SharpGLTF.Schema2;
using Xunit.Abstractions;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    public class FinGltfGameDataTests
    {
        // Models are about 50 units tall; this is float noise after inverting and multiplying the bone matrices
        private const float MAX_REST_POSE_ERROR = 1e-3f;
        private static readonly string[] TEXTURE_ARCHIVES = ["Map", "LMap"];

        private readonly ITestOutputHelper _output;

        public FinGltfGameDataTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Reading the output back with strict validation checks it against the glTF schema and its data rules
        [GameDataFact]
        public void ToGlb_ProducesValidGltf_ForEveryShippedFile()
        {
            // Arrange
            var failures = new List<string>();
            var files = FinGameDataTests
                .FinFiles()
                .ToList();
            var meshesWithoutNormals = 0;

            // Act
            for (var i = 0; i < files.Count; i++)
            {
                var (name, data) = files[i];
                var file = FinReader.Read(name, data);
                foreach (var includeAll in new[] { false, true })
                {
                    try
                    {
                        var glb = new FinToGltfConverter(includeAll, includeAll).ToGlb(file);
                        var model = GlbTestReader.Read(glb);
                        if (!includeAll)
                        {
                            meshesWithoutNormals += model.LogicalMeshes
                                .Count(mesh => mesh.Primitives.Any(e => e.GetVertexAccessor("NORMAL") is null));
                        }
                    }
                    catch (Exception e)
                    {
                        failures.Add($"{name} (include all: {includeAll}): {e.GetType().Name}: {e.Message}");
                    }
                }
            }

            _output.WriteLine($"{files.Count} files, {meshesWithoutNormals} meshes without normals");
            foreach (var failure in failures)
            {
                _output.WriteLine(failure);
            }

            // Assert
            Assert.NotEmpty(files);
            Assert.Empty(failures);
        }

        // As exported for the port (every node kept): each animation is a skill, its values are numbers, and every
        // pointer and flip-book refers to something in the file
        [GameDataFact]
        public void Animations_FitTheirModel_ForEveryShippedFile()
        {
            // Arrange
            var failures = new List<string>();
            var skills = 0;
            var animations = 0;
            var flipBooks = 0;

            // Act
            foreach (var (name, data) in FinGameDataTests.FinFiles())
            {
                var file = FinReader.Read(name, data);
                var glb = new FinToGltfConverter(true, true).ToGlb(file);
                var model = GlbTestReader.Read(glb);
                var json = GlbTestReader.Json(glb);
                var skillNames = file.Objects.OfType<DDActorSharedData>().SelectMany(e => e.Skills).Select(e => e.Name).ToList();
                skills += skillNames.Count;
                animations += model.LogicalAnimations.Count;
                foreach (var animation in model.LogicalAnimations)
                {
                    if (!skillNames.Contains(animation.Name))
                    {
                        failures.Add($"{name}: animation {animation.Name} isn't a skill");
                    }
                    foreach (var channel in animation.Channels)
                    {
                        if (channel.TargetPointerPath is { } pointer && !PointsIntoModel(pointer, model))
                        {
                            failures.Add($"{name}: {animation.Name} points at {pointer}");
                        }
                    }
                }
                foreach (var animation in json["animations"]?.AsArray() ?? [])
                {
                    foreach (var sampler in animation!["samplers"]!.AsArray())
                    {
                        var output = model.LogicalAccessors[sampler!["output"]!.GetValue<int>()];
                        if (output.Encoding == EncodingType.FLOAT && Floats(output).Any(value => !float.IsFinite(value)))
                        {
                            failures.Add($"{name}: {animation["name"]} has a value that isn't a finite number");
                        }
                    }
                }
                var root = json["nodes"]!.AsArray().First(node => node!["extras"]?["file"] is not null)!;
                foreach (var flipBook in root["extras"]!["flipbooks"]?.AsArray() ?? [])
                {
                    flipBooks++;
                    var images = flipBook!["images"]!.AsArray().Select(e => e!.GetValue<int>()).ToList();
                    var frames = flipBook["frames"]!.AsArray().Select(e => e!.GetValue<int>()).ToList();
                    if (images.Any(i => i < 0 || i >= model.LogicalImages.Count) || frames.Any(f => f < 0 || f >= images.Count)
                        || flipBook["material"]!.GetValue<int>() >= model.LogicalMaterials.Count)
                    {
                        failures.Add($"{name}: flip-book of {flipBook["skill"]} refers past the model");
                    }
                }
            }

            _output.WriteLine($"{skills} skills, {animations} animations, {flipBooks} flip-books");
            foreach (var failure in failures)
            {
                _output.WriteLine(failure);
            }

            // Assert
            Assert.NotEqual(0, animations);
            Assert.NotEqual(0, flipBooks);
            Assert.Empty(failures);
        }

        private static IEnumerable<float> Floats(Accessor accessor)
        {
            return accessor.Dimensions switch
            {
                DimensionType.VEC3 => accessor.AsVector3Array().SelectMany(v => new[] { v.X, v.Y, v.Z }),
                DimensionType.VEC4 => accessor.AsVector4Array().SelectMany(v => new[] { v.X, v.Y, v.Z, v.W }),
                _ => accessor.AsScalarArray(),
            };
        }

        // /nodes/N/... or /materials/N/... with N inside the model
        private static bool PointsIntoModel(string pointer, ModelRoot model)
        {
            var parts = pointer.Split('/');
            if (parts.Length < 3 || !int.TryParse(parts[2], out var index))
            {
                return false;
            }

            return parts[1] switch
            {
                "nodes" => index < model.LogicalNodes.Count,
                "materials" => index < model.LogicalMaterials.Count,
                _ => false,
            };
        }

        [GameDataFact]
        public void Environment_HasPunctualLights()
        {
            // Arrange
            var (name, data) = FinGameDataTests
                .FinFiles()
                .First(e => e.Name.Equals("E1010", StringComparison.OrdinalIgnoreCase));
            var file = FinReader.Read(name, data);

            // Act
            var glb = new FinToGltfConverter(true, true).ToGlb(file);

            // Assert
            var lights = GlbTestReader.Json(glb)["extensions"]?["KHR_lights_punctual"]?["lights"]?.AsArray();
            Assert.NotNull(lights);
            Assert.NotEmpty(lights);
        }

        // As exported for the port; AGENTS.md of the port states its 4 × 4 studs, which the port's tests check
        [GameDataFact]
        public void DirectionTile_HasOneBoxCollider()
        {
            // Arrange
            var (name, data) = FinGameDataTests
                .FinFiles()
                .First(e => e.Name.Equals("U0179", StringComparison.OrdinalIgnoreCase));
            var file = FinReader.Read(name, data);

            // Act
            var glb = new FinToGltfConverter(true, true).ToGlb(file);

            // Assert
            var shapes = GlbTestReader.Json(glb)["extensions"]?["OMI_physics_shape"]?["shapes"]?.AsArray();
            Assert.NotNull(shapes);
            var shape = Assert.Single(shapes);
            Assert.Equal("box", shape!["type"]!.GetValue<string>());
        }

        // As exported for the port: every model converts, and every collider names a shape of its own glb
        [GameDataFact]
        public void EveryModel_Converts_WithValidColliderIndices()
        {
            // Arrange
            var failures = new List<string>();
            var files = FinGameDataTests
                .FinFiles()
                .ToList();
            var colliders = 0;

            // Act
            foreach (var (name, data) in files)
            {
                try
                {
                    var glb = new FinToGltfConverter(true, true).ToGlb(FinReader.Read(name, data));
                    GlbTestReader.Read(glb);
                    var json = GlbTestReader.Json(glb);
                    var shapeCount = json["extensions"]?["OMI_physics_shape"]?["shapes"]?.AsArray().Count ?? 0;
                    var nodes = json["nodes"]?.AsArray() ?? [];
                    foreach (var node in nodes)
                    {
                        if (node!["extensions"]?["OMI_physics_body"]?["collider"]?["shape"] is not { } shape)
                        {
                            continue;
                        }
                        colliders++;
                        var index = shape.GetValue<int>();
                        if (index < 0 || index >= shapeCount)
                        {
                            failures.Add($"{name}: collider shape {index} of {shapeCount}");
                        }
                    }
                }
                catch (Exception e)
                {
                    failures.Add($"{name}: {e.GetType().Name}: {e.Message}");
                }
            }

            _output.WriteLine($"{files.Count} files, {colliders} colliders");
            foreach (var failure in failures)
            {
                _output.WriteLine(failure);
            }

            // Assert
            Assert.Equal(147, files.Count);
            Assert.NotEqual(0, colliders);
            Assert.Empty(failures);
        }

        // With every level of detail and every hidden node kept, no geometry may be lost: each shape or corona with
        // vertices gets its own mesh
        [GameDataFact]
        public void ToGlb_WritesEveryShape_WhenEverythingIsIncluded()
        {
            // Arrange
            var failures = new List<string>();

            // Act
            foreach (var (name, data) in FinGameDataTests.FinFiles())
            {
                var file = FinReader.Read(name, data);
                var shapes = file.Objects
                    .OfType<NiTriBasedGeom>()
                    .Count(e => e.Vertices is { Length: > 0 });
                var model = new FinToGltfConverter(includeAllLevelsOfDetail: true, includeHidden: true).ToModel(file);
                if (model.LogicalMeshes.Count != shapes)
                {
                    failures.Add($"{name}: {shapes} shapes with vertices, but {model.LogicalMeshes.Count} meshes");
                }
            }

            foreach (var failure in failures)
            {
                _output.WriteLine(failure);
            }

            // Assert
            Assert.Empty(failures);
        }

        // Every skin is bound to its bones, and at rest skinning leaves each vertex where the baked mesh puts it
        [GameDataFact]
        public void ToGlb_BindsEverySkin_AtItsRestPose()
        {
            // Arrange
            var failures = new List<string>();
            var skins = 0;

            // Act
            foreach (var (name, data) in FinGameDataTests.FinFiles())
            {
                var model = new FinToGltfConverter().ToModel(FinReader.Read(name, data));
                var nodes = model.LogicalNodes
                    .Where(e => e.Mesh?.Primitives[0].GetVertexAccessor("JOINTS_0") is not null)
                    .ToList();
                foreach (var node in nodes)
                {
                    skins++;
                    if (node.Skin is null)
                    {
                        failures.Add($"{name} {node.Name}: has joints but no skin");
                        continue;
                    }

                    var primitive = node.Mesh.Primitives[0];
                    var positions = primitive
                        .GetVertexAccessor("POSITION")
                        .AsVector3Array();
                    var joints = primitive
                        .GetVertexAccessor("JOINTS_0")
                        .AsVector4Array();
                    for (var i = 0; i < positions.Count; i++)
                    {
                        var (joint, inverseBindMatrix) = node.Skin.GetJoint((int)joints[i].X);
                        var skinned = Vector3.Transform(positions[i], inverseBindMatrix * joint.WorldMatrix);
                        var baked = Vector3.Transform(positions[i], node.WorldMatrix);
                        if (Vector3.Distance(skinned, baked) > MAX_REST_POSE_ERROR)
                        {
                            failures.Add($"{name} {node.Name}: vertex {i} moves {Vector3.Distance(skinned, baked)} at rest");
                            break;
                        }
                    }
                }
            }

            _output.WriteLine($"{skins} skinned meshes");
            foreach (var failure in failures)
            {
                _output.WriteLine(failure);
            }

            // Assert
            Assert.NotEqual(0, skins);
            Assert.Empty(failures);
        }
    
        // Textures are converted separately from Map.pac and LMap.pac, so every image a model names must be one of them
        [GameDataFact]
        public void ToGlb_ReferencesOnlyShippedTextures_ForEveryModel()
        {
            // Arrange
            var reader = new PacArchiveReader();
            var textures = GameDataPaths
                .Archives()
                .Where(e => TEXTURE_ARCHIVES.Contains(Path.GetFileNameWithoutExtension(e), StringComparer.OrdinalIgnoreCase))
                .SelectMany(e => reader.Read(e).Entries.Keys)
                .Where(e => e.FileName.EndsWith(".tga", StringComparison.OrdinalIgnoreCase))
                .Select(e => FinToGltfConverter.DEFAULT_TEXTURE_URI_PREFIX + AssetFileName.WithExtension(e.FileName, "png"))
                .ToHashSet();
            var unknown = new SortedSet<string>();
            var referenced = 0;

            // Act
            foreach (var (name, data) in FinGameDataTests.FinFiles())
            {
                var glb = new FinToGltfConverter(true, true).ToGlb(FinReader.Read(name, data));
                foreach (var uri in GlbTestReader.ImageUris(glb))
                {
                    referenced++;
                    if (!textures.Contains(uri))
                    {
                        unknown.Add($"{name}: {uri}");
                    }
                }
            }

            // Assert
            Assert.NotEqual(0, referenced);
            Assert.Empty(unknown);
        }

        // Every lightmapped shape in the game is an environment piece with baked vertex colours and no normals
        [GameDataFact]
        public void ToGlb_GivesEveryLightmappedShapeAnUnlitMaterial()
        {
            // Arrange
            var lightmapped = 0;
            var lit = new List<string>();

            // Act
            foreach (var (name, data) in FinGameDataTests.FinFiles())
            {
                var model = new FinToGltfConverter(true, true).ToModel(FinReader.Read(name, data));
                foreach (var material in model.LogicalMaterials)
                {
                    if (material.Extras is JsonObject extras && extras.ContainsKey("lightmap"))
                    {
                        lightmapped++;
                        if (!material.Unlit)
                        {
                            lit.Add($"{name}: {material.Name}");
                        }
                    }
                }
            }

            // Assert
            Assert.NotEqual(0, lightmapped);
            Assert.Empty(lit);
        }
}
}
