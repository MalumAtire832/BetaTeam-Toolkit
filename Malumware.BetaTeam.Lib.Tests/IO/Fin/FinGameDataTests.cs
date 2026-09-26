using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using Malumware.BetaTeam.Lib.IO.Pac;
using Malumware.BetaTeam.Lib.Tests.GameData;
using Xunit.Abstractions;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinGameDataTests
    {
        private readonly ITestOutputHelper _output;

        public FinGameDataTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Parsing without an exception already proves the stream ends exactly at End Of File and every link resolves
        [GameDataFact]
        public void Read_ParsesCompletely_ForEveryShippedFile()
        {
            // Arrange
            var failures = new List<string>();
            var count = 0;

            // Act
            foreach (var (name, data) in FinFiles())
            {
                count++;
                try
                {
                    var file = FinReader.Read(name, data);
                    if (file.TopLevelObjects.Count == 0)
                    {
                        failures.Add($"{name}: no top-level object");
                    }
                }
                catch (InvalidDataException e)
                {
                    failures.Add($"{name}: {e.Message}");
                }
            }

            foreach (var failure in failures)
            {
                _output.WriteLine(failure);
            }

            // Assert
            Assert.NotEqual(0, count);
            Assert.Empty(failures);
        }

        [GameDataFact]
        public void Read_ReturnsTrianglesWithinVertexCount_ForEveryShippedShape()
        {
            // Arrange
            var failures = new List<string>();
            var shapes = 0;

            // Act
            foreach (var (name, data) in FinFiles())
            {
                foreach (var shape in FinReader.Read(name, data).Objects.OfType<NiTriShape>())
                {
                    shapes++;
                    var outOfRange = shape.Triangles.Count(t => t.A >= shape.VertexCount || t.B >= shape.VertexCount || t.C >= shape.VertexCount);
                    if (outOfRange > 0)
                    {
                        failures.Add($"{name}: {shape.ClassName} at 0x{shape.Offset:X} has {outOfRange} triangles past vertex {shape.VertexCount}");
                    }
                }
            }

            // Assert
            Assert.NotEqual(0, shapes);
            Assert.Empty(failures);
        }

        internal static IEnumerable<(string Name, byte[] Data)> FinFiles()
        {
            var reader = new PacArchiveReader();
            foreach (var path in GameDataPaths.Archives())
            {
                if (!Path.GetFileName(path).Equals("Fin.pac", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var archive = reader.Read(path);
                foreach (var (entry, data) in archive.Entries.OrderBy(pair => pair.Key.FileName))
                {
                    if (entry.FileName.EndsWith(".FIN", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return (Path.GetFileNameWithoutExtension(entry.FileName), data);
                    }
                }
            }
        }
    }
}
