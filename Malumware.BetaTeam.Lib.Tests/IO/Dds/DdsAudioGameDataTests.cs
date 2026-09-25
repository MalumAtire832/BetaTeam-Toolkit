using Malumware.BetaTeam.Lib.IO.Dds;
using Malumware.BetaTeam.Lib.IO.Pac;
using Malumware.BetaTeam.Lib.Tests.GameData;

namespace Malumware.BetaTeam.Lib.Tests.IO.Dds
{
    public class DdsAudioGameDataTests
    {
        [GameDataFact]
        public void Read_ProducesWholeSampleFrames_ForEveryShippedFile()
        {
            // Arrange
            var reader = new PacArchiveReader();
            var misaligned = new List<string>();
            var count = 0;

            // Act
            foreach (var archivePath in GameDataPaths.Archives())
            {
                var archive = reader.Read(archivePath);
                foreach (var (entry, data) in archive.Entries)
                {
                    if (!entry.FileName.EndsWith(".DDS", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var audio = DdsAudioReader.Read(entry.FileName, data);
                    count++;

                    if (audio.Data.Length % audio.Header.BlockAlign != 0)
                    {
                        misaligned.Add($"{archive.FileName}/{entry.FileName}");
                    }
                }
            }

            // Assert
            Assert.NotEqual(0, count);
            Assert.Empty(misaligned);
        }
    }
}
