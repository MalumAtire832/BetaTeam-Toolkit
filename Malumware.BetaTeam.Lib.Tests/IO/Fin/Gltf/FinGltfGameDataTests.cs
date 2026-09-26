using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;
using Malumware.BetaTeam.Lib.Tests.GameData;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using Xunit.Abstractions;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    public class FinGltfGameDataTests
    {
        // Models are about 50 units tall; this is float noise after inverting and multiplying the bone matrices
        private const float MAX_REST_POSE_ERROR = 1e-3f;

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
            var settings = new ReadSettings { Validation = ValidationMode.Strict };
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
                        var model = ModelRoot.ParseGLB(glb, settings);
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
    }
}
