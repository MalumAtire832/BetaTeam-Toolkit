namespace Malumware.BetaTeam.Lib.Tests.GameData
{
    public static class GameDataPaths
    {
        private static EnumerationOptions ENUMERATION_OPTIONS = new() { MatchCasing = MatchCasing.CaseInsensitive };
        public const string ENVIRONMENT_VARIABLE = "ALPHATEAM_GAME_DIR";

        public static string? GameDirectory { get; } = FindGameDirectory();

        public static IEnumerable<string> Archives()
        {
            if (GameDirectory is null)
            {
                return [];
            }

            return Directory.EnumerateFiles(GameDirectory, "*.pac", ENUMERATION_OPTIONS)
                .Order();
        }

        private static string? FindGameDirectory()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(ENVIRONMENT_VARIABLE);
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                return ContainsArchives(fromEnvironment) ? fromEnvironment : null;
            }

            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "BetaTeam.sln")))
                {
                    var candidate = Path.Combine(dir.FullName, "Research", "game");
                    return ContainsArchives(candidate) ? candidate : null;
                }
            }

            return null;
        }

        private static bool ContainsArchives(string directory)
        {
            return Directory.Exists(directory)
                && Directory.EnumerateFiles(directory, "*.pac", ENUMERATION_OPTIONS).Any();
        }
    }
}
