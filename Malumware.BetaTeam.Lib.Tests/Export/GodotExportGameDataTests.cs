using Malumware.BetaTeam.Lib.Export;
using Malumware.BetaTeam.Lib.Tests.GameData;

namespace Malumware.BetaTeam.Lib.Tests.Export
{
    public class GodotExportGameDataTests
    {
        [GameDataFact]
        public void Run_ExportsTheWholeGame_WithoutErrors()
        {
            // Arrange
            var output = Path.Combine(Path.GetTempPath(), $"godot-export-{Guid.NewGuid():N}");
            try
            {
                var files = GameFiles.FromGameDirectory(GameDataPaths.GameDirectory!);

                // Act
                var first = new GodotExport().Run(files, output);
                var second = new GodotExport().Run(files, output);

                // Assert
                Assert.Empty(first.Errors);
                Assert.Equal(147, Directory.GetFiles(Path.Combine(output, "models"), "*.glb").Length);
                Assert.Equal(1260, Directory.GetFiles(Path.Combine(output, "textures"), "*.png").Length);
                Assert.Equal(111, Directory.GetFiles(Path.Combine(output, "levels"), "*.puz").Length);
                Assert.Equal(3, Directory.GetFiles(Path.Combine(output, "data"), "*.txt").Length);
                Assert.Equal(498, Directory.GetFiles(Path.Combine(output, "audio"), "*.wav").Length);
                Assert.Equal(["alphateam.pot", "en.po"], Directory.GetFiles(Path.Combine(output, "text")).Select(Path.GetFileName).Order().ToArray());
                Assert.Equal(0, second.Written);
                Assert.Equal(first.Written, second.Skipped);
            }
            finally
            {
                if (Directory.Exists(output))
                {
                    Directory.Delete(output, true);
                }
            }
        }
    }
}
