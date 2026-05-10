using System.Text;
using Malumware.BetaTeam.Lib.IO.Pac;

namespace Malumware.BetaTeam.Lib.Tests.IO.Pac
{
    public class PacArchiveHeaderTests
    {
        [Fact]
        public void IsValid_ReturnsTrue_WhenMagicIsPack()
        {
            // Arrange
            var magic = Encoding.ASCII.GetBytes("PACK");
            var header = new PacArchiveHeader(magic, archiveSize: 0, payloadOffset: 0, fileCount: 0);

            // Act
            var isValid = header.IsValid;

            // Assert
            Assert.True(isValid);
        }

        [Theory]
        [InlineData("NWVS")]
        [InlineData("pack")]
        [InlineData("nope")]
        [InlineData("")]
        public void IsValid_ReturnsFalse_WhenMagicIsNotPack(string magicAscii)
        {
            // Arrange
            var magic = Encoding.ASCII.GetBytes(magicAscii);
            var header = new PacArchiveHeader(magic, archiveSize: 0, payloadOffset: 0, fileCount: 0);

            // Act
            var isValid = header.IsValid;

            // Assert
            Assert.False(isValid);
        }
    }
}
