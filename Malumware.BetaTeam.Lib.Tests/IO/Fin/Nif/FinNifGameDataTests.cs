using System.Text.RegularExpressions;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Nif;
using Malumware.BetaTeam.Lib.IO.Nif;
using Malumware.BetaTeam.Lib.Tests.GameData;
using Xunit.Abstractions;
using FinBlocks = Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using NifBlocks = Malumware.BetaTeam.Lib.IO.Nif.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Nif
{
    public class FinNifGameDataTests
    {
        private readonly ITestOutputHelper _output;

        public FinNifGameDataTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Every shape in a shipped file hangs in the scene tree, so every one should reach the NIF with all of its
        // triangles. The warnings are written to the test output as a record of what the export leaves out.
        [GameDataFact]
        public void Convert_ExportsEveryShape_ForEveryShippedFile()
        {
            // Arrange
            var failures = new List<string>();
            var warnings = new Dictionary<string, int>();
            var count = 0;

            // Act
            foreach (var (name, data) in FinGameDataTests.FinFiles())
            {
                count++;
                var file = FinReader.Read(name, data);
                var conversion = new FinToNifConverter().Convert(file);
                NifWriter.Write(conversion.File);
                // Counted per file, without the count of repeats within the file
                foreach (var warning in conversion.Warnings.Select(e => Regex.Replace(e, @" \(\d+x\)$", "")))
                {
                    warnings[warning] = warnings.GetValueOrDefault(warning) + 1;
                }

                var finShapes = file.Objects
                    .OfType<FinBlocks.NiTriBasedGeom>()
                    .ToList();
                var nifShapes = conversion.File
                    .CollectBlocks()
                    .OfType<NifBlocks.NiTriShape>()
                    .ToList();
                var finTriangles = finShapes
                    .OfType<FinBlocks.NiTriShape>()
                    .Sum(e => e.Triangles.Length);
                var nifTriangles = nifShapes.Sum(e => ((NifBlocks.NiTriShapeData)e.Data!).Triangles.Length);
                if (finShapes.Count != nifShapes.Count || finTriangles != nifTriangles)
                {
                    failures.Add(
                        $"{name}: {finShapes.Count} shapes with {finTriangles} triangles, " +
                        $"exported {nifShapes.Count} with {nifTriangles}"
                    );
                }
            }

            foreach (var (warning, files) in warnings.OrderByDescending(e => e.Value))
            {
                _output.WriteLine($"{files} files: {warning}");
            }
            foreach (var failure in failures)
            {
                _output.WriteLine(failure);
            }

            // Assert
            Assert.NotEqual(0, count);
            Assert.Empty(failures);
        }
    }
}
