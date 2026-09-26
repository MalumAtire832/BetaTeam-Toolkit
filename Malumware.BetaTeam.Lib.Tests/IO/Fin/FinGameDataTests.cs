using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Pac;
using Malumware.BetaTeam.Lib.Tests.GameData;
using Xunit.Abstractions;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinGameDataTests
    {
        // Classes that don't have a reader yet. A file may only stop at one of these; any other failure,
        // including an unknown name that isn't listed, usually means an earlier block was misread.
        // Each class group removes its names. A name is added only after confirming the engine registers it.
        private static readonly HashSet<string> PENDING_CLASSES =
        [
            "DDUnit", "DDActorSharedData", "DDEnv", "DDCorona",
        ];

        private readonly ITestOutputHelper _output;

        public FinGameDataTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [GameDataFact]
        public void Read_ParsesOrStopsAtPendingClass_ForEveryShippedFile()
        {
            // Arrange
            var failures = new List<string>();
            var stoppedAt = new Dictionary<string, int>();
            var parsed = 0;
            var count = 0;

            // Act
            foreach (var (name, data) in FinFiles())
            {
                count++;
                try
                {
                    FinReader.Read(name, data);
                    parsed++;
                }
                catch (InvalidDataException e)
                {
                    if (FinUnknownClass.TryGet(e, out var className, out _) && PENDING_CLASSES.Contains(className))
                    {
                        stoppedAt[className] = stoppedAt.GetValueOrDefault(className) + 1;
                    }
                    else
                    {
                        failures.Add($"{name}: {e.Message}");
                    }
                }
            }

            // Progress, visible with `dotnet test --logger "console;verbosity=detailed"`
            _output.WriteLine($"{parsed} of {count} files parse completely");
            foreach (var (className, files) in stoppedAt.OrderByDescending(pair => pair.Value))
            {
                _output.WriteLine($"  {files,4} stop at {className}");
            }
            foreach (var failure in failures)
            {
                _output.WriteLine(failure);
            }

            // Assert
            Assert.NotEqual(0, count);
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
