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
                foreach (var allLevelsOfDetail in new[] { false, true })
                {
                    try
                    {
                        var glb = new FinToGltfConverter(allLevelsOfDetail).ToGlb(file);
                        var model = ModelRoot.ParseGLB(glb, settings);
                        if (!allLevelsOfDetail)
                        {
                            meshesWithoutNormals += model.LogicalMeshes
                                .Count(mesh => mesh.Primitives.Any(e => e.GetVertexAccessor("NORMAL") is null));
                        }
                    }
                    catch (Exception e)
                    {
                        failures.Add($"{name} (all LODs: {allLevelsOfDetail}): {e.GetType().Name}: {e.Message}");
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

        // With every level of detail kept, no geometry may be lost: each shape or corona with vertices gets its own mesh
        [GameDataFact]
        public void ToGlb_WritesEveryShape_WhenAllLevelsOfDetailIsSet()
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
                var model = new FinToGltfConverter(allLevelsOfDetail: true).ToModel(file);
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
    }
}
