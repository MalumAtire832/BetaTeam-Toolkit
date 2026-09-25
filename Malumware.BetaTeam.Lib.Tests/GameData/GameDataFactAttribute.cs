namespace Malumware.BetaTeam.Lib.Tests.GameData
{
    /// <summary>
    /// A fact that runs against the original game files, and is skipped when they are not available.
    /// The game files are not part of the repository, see <see cref="GameDataPaths"/> for where they are looked up.
    /// </summary>
    public sealed class GameDataFactAttribute : FactAttribute
    {
        public GameDataFactAttribute()
        {
            if (GameDataPaths.GameDirectory is null)
            {
                Skip = $"Game files not found. Set {GameDataPaths.ENVIRONMENT_VARIABLE} or place them in Research/game.";
            }
        }
    }
}
