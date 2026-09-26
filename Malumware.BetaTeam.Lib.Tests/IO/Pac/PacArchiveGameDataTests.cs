using Malumware.BetaTeam.Lib.IO.Pac;
using Malumware.BetaTeam.Lib.Tests.GameData;

namespace Malumware.BetaTeam.Lib.Tests.IO.Pac
{
    public class PacArchiveGameDataTests
    {
        [GameDataFact]
        public void Read_AccountsForEveryByte_OfEveryShippedArchive()
        {
            // Arrange
            var reader = new PacArchiveReader();
            var archivePaths = GameDataPaths
                .Archives()
                .ToList();

            // Act & Assert
            Assert.NotEmpty(archivePaths);
            foreach (var archivePath in archivePaths)
            {
                var length = new FileInfo(archivePath).Length;
                var archive = reader.Read(archivePath);

                Assert.Equal(length, archive.Header.ArchiveSize);

                // File data is stored back to back after the directory, and ends exactly at the end of the archive.
                var expectedOffset = (long)archive.Header.DirectorySize;
                foreach (var entry in archive.Entries.Keys.OrderBy(e => e.Offset))
                {
                    Assert.Equal(expectedOffset, entry.Offset);
                    expectedOffset += entry.Size;
                }
                Assert.Equal(length, expectedOffset);
            }
        }
    }
}
