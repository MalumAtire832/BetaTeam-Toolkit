using System.Text;
using Malumware.BetaTeam.Lib.IO.Locale;
using Malumware.BetaTeam.Lib.IO.Pac;
using Malumware.BetaTeam.Lib.Tests.GameData;

namespace Malumware.BetaTeam.Lib.Tests.IO.Locale
{
    public class StringTableGameDataTests
    {
        [GameDataFact]
        public void Read_ReturnsEntryForEveryTagLine_ForEveryShippedFile()
        {
            // Arrange
            var reader = new PacArchiveReader();
            var mismatches = new List<string>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new List<string>();
            var count = 0;

            // Act
            foreach (var archive in LocaleArchives(reader))
            {
                foreach (var (entry, data) in archive.Entries)
                {
                    if (!entry.FileName.EndsWith(".TXT", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var table = StringTableReader.Read(entry.FileName, data);
                    count++;

                    var tagLines = Encoding.ASCII
                        .GetString(data)
                        .Split('\n')
                        .Count(line => line.StartsWith("=="));
                    if (table.Entries.Count != tagLines)
                    {
                        mismatches.Add($"{archive.FileName}/{entry.FileName}: {table.Entries.Count} of {tagLines}");
                    }

                    // The game merges all files of an archive into one table, so a repeated key would be ignored
                    foreach (var tableEntry in table.Entries)
                    {
                        if (!keys.Add(tableEntry.Key))
                        {
                            duplicates.Add($"{archive.FileName}/{entry.FileName}: {tableEntry.Key}");
                        }
                    }
                }
            }

            // Assert
            Assert.NotEqual(0, count);
            Assert.Empty(mismatches);
            Assert.Empty(duplicates);
        }

        [GameDataFact]
        public void ToPo_WritesEveryKey_ForTheShippedTables()
        {
            // Arrange
            var reader = new PacArchiveReader();
            var tables = new List<StringTable>();
            foreach (var archive in LocaleArchives(reader).Where(a => a.FileName.Contains("Locale_en", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var (entry, data) in archive.Entries)
                {
                    var name = Path.GetFileNameWithoutExtension(entry.FileName);
                    if (entry.FileName.EndsWith(".TXT", StringComparison.OrdinalIgnoreCase)
                        && !name.Equals("KBD_EN", StringComparison.OrdinalIgnoreCase))
                    {
                        tables.Add(StringTableReader.Read(name, data));
                    }
                }
            }

            // Act
            var po = GettextWriter.ToPo(tables, "en");

            // Assert
            var msgids = po
                .Split('\n')
                .Count(line => line.StartsWith("msgid \"") && line != "msgid \"\"");
            Assert.Equal(500, msgids);
        }

        private static IEnumerable<PacArchive> LocaleArchives(PacArchiveReader reader)
        {
            return GameDataPaths
                .Archives()
                .Where(path => Path.GetFileName(path).StartsWith("Locale_", StringComparison.OrdinalIgnoreCase))
                .Select(reader.Read);
        }
    }
}
