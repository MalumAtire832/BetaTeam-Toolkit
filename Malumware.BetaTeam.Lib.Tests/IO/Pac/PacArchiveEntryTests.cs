using Malumware.BetaTeam.Lib.IO.Pac;

namespace Malumware.BetaTeam.Lib.Tests.IO.Pac
{
    public class PacArchiveEntryTests
    {
        [Fact]
        public void GetDestinationPath_ReturnsPathInsideDirectory()
        {
            // Arrange
            var directory = Path.Combine(Path.GetTempPath(), "out");
            var entry = new PacArchiveEntry(@"SUB\FOO.TGA", 0, 0, null);

            // Act
            var path = entry.GetDestinationPath(directory);

            // Assert
            Assert.Equal(Path.Combine(Path.GetFullPath(directory), "SUB", "FOO.TGA"), path);
        }

        [Theory]
        [InlineData(@"..\EVIL.TXT")]
        [InlineData(@"SUB\..\..\EVIL.TXT")]
        [InlineData(@"..\out-sibling\EVIL.TXT")]
        public void GetDestinationPath_ThrowsInvalidDataException_WhenEntryEscapesDirectory(string fileName)
        {
            // Arrange
            var directory = Path.Combine(Path.GetTempPath(), "out");
            var entry = new PacArchiveEntry(fileName, 0, 0, null);

            // Act
            var act = () => entry.GetDestinationPath(directory);

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }
    }
}
