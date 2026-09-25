namespace Malumware.BetaTeam.Lib.Tests.GameData
{
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
