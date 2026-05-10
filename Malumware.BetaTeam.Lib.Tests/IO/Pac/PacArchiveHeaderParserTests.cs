using Malumware.BetaTeam.Lib.IO.Pac;

namespace Malumware.BetaTeam.Lib.Tests.IO.Pac
{
    public class PacArchiveHeaderParserTests
    {
        [Fact]
        public void Parse_ReturnsHeader_WhenBufferIsValid()
        {
            // Arrange
            var bytes = new byte[]
            {
                (byte)'P', (byte)'A', (byte)'C', (byte)'K', // magic
                0x40, 0x00, 0x00, 0x00,                     // archive size = 0x40
                0x00, 0x00, 0x00, 0x00,                     // reserved
                0x14, 0x00, 0x00, 0x00,                     // payload offset = 0x14
                0x02, 0x00, 0x00, 0x00,                     // file count = 2
            };
            var stream = new MemoryStream(bytes);
            using var parser = new PacArchiveHeaderParser(stream);

            // Act
            var header = parser.Parse();

            // Assert
            Assert.True(header.IsValid);
            Assert.Equal(0x40u, header.ArchiveSize);
            Assert.Equal(0x14u, header.PayloadOffset);
            Assert.Equal(2u, header.FileCount);
        }

        [Fact]
        public void Parse_ThrowsInvalidDataException_WhenMagicIsBad()
        {
            // Arrange
            var bytes = new byte[20];
            bytes[0] = (byte)'N';
            bytes[1] = (byte)'O';
            bytes[2] = (byte)'P';
            bytes[3] = (byte)'E';
            var stream = new MemoryStream(bytes);
            using var parser = new PacArchiveHeaderParser(stream);

            // Act
            var act = () => parser.Parse();

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }
    }
}
