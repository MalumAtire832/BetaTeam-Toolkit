using Malumware.BetaTeam.Lib.IO.Pac;

namespace Malumware.BetaTeam.Lib.Export
{
    public static class GameFiles
    {
        // The archives the Godot export reads from
        public static readonly string[] ARCHIVES = ["Fin", "Map", "LMap", "Etc"];

        private static readonly EnumerationOptions ENUMERATION_OPTIONS = new() { MatchCasing = MatchCasing.CaseInsensitive };

        // Every file in the exported archives. A file counts as changed when its archive does, so each takes the
        // archive's last write time.
        public static IReadOnlyList<GameFile> FromGameDirectory(string gameDirectory)
        {
            var reader = new PacArchiveReader();
            var result = new List<GameFile>();
            foreach (var archive in ARCHIVES)
            {
                var path = Directory
                    .EnumerateFiles(gameDirectory, $"{archive}.pac", ENUMERATION_OPTIONS)
                    .FirstOrDefault();
                if (path is null)
                {
                    throw new FileNotFoundException($"{archive}.pac is missing from {gameDirectory}");
                }

                var modified = File.GetLastWriteTimeUtc(path);
                foreach (var (entry, data) in reader.Read(path).Entries)
                {
                    var name = entry.FileName[(entry.FileName.LastIndexOf(PacArchiveEntry.PATH_SEPARATOR) + 1)..];
                    result.Add(new GameFile(archive, name, modified, () => data));
                }
            }

            return result;
        }
    }
}
