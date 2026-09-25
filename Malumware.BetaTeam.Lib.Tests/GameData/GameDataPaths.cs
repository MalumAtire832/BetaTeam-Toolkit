namespace Malumware.BetaTeam.Lib.Tests.GameData
{
    /// <summary>
    /// Locates an installed copy of LEGO Alpha Team for tests that run against the original game files.
    /// </summary>
    public static class GameDataPaths
    {
        /// <summary>Environment variable pointing at the game's install directory.</summary>
        public const string ENVIRONMENT_VARIABLE = "BETATEAM_GAME_DIR";

        /// <summary>
        /// The game's install directory, or <c>null</c> when it cannot be found. Checks
        /// <see cref="ENVIRONMENT_VARIABLE"/> first, then <c>Research/game</c> in the repository root.
        /// </summary>
        public static string? GameDirectory { get; } = FindGameDirectory();

        public static IEnumerable<string> Archives()
        {
            if (GameDirectory is null)
            {
                return [];
            }

            return Directory.EnumerateFiles(GameDirectory, "*.pac", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
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
                && Directory.EnumerateFiles(directory, "*.pac", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }).Any();
        }
    }
}
