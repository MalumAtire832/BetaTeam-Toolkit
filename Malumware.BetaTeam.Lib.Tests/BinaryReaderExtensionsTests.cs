using Malumware.Common;

namespace Malumware.BetaTeam.Lib.Tests
{
    public class BinaryReaderExtensionsTests
    {
        [Fact]
        public void ReadBytesUntil_ReadsBytes_AndConsumesTerminator()
        {
            // Arrange
            var bytes = new byte[] { 0x41, 0x42, 0x43, 0x00, 0x44 };
            using var reader = new BinaryReader(new MemoryStream(bytes));

            // Act
            var result = reader.ReadBytesUntil(terminator: 0x00);

            // Assert
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, result);
            Assert.Equal(0x44, reader.ReadByte());
        }

        [Fact]
        public void ReadBytesUntil_ReturnsEmpty_WhenTerminatorIsFirstByte()
        {
            // Arrange
            var bytes = new byte[] { 0x00, 0x41 };
            using var reader = new BinaryReader(new MemoryStream(bytes));

            // Act
            var result = reader.ReadBytesUntil(terminator: 0x00);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void ReadBytesUntil_ThrowsEndOfStream_WhenTerminatorIsMissing()
        {
            // Arrange
            var bytes = new byte[] { 0x41, 0x42 };
            using var reader = new BinaryReader(new MemoryStream(bytes));

            // Act
            var act = () => reader.ReadBytesUntil(terminator: 0x00);

            // Assert
            Assert.Throws<EndOfStreamException>(act);
        }
    }
}
