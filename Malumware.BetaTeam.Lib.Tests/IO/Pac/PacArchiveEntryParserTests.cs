using System.Text;
using Malumware.BetaTeam.Lib.IO.Pac;

namespace Malumware.BetaTeam.Lib.Tests.IO.Pac
{
    public class PacArchiveEntryParserTests
    {
        [Fact]
        public void Parse_ReturnsMetadata_FromDirectoryEntryBuffer()
        {
            // Arrange
            var bytes = BuildEntryBytes(
                fileName: "FOO",
                offset: 0x44u,
                size: 3u,
                fileTime: 0L);
            using var parser = new PacArchiveEntryParser(new MemoryStream(bytes));

            // Act
            var entry = parser.Parse();

            // Assert
            Assert.Equal("FOO", entry.FileName);
            Assert.Equal(0x44L, entry.Offset);
            Assert.Equal(3, entry.Size);
            Assert.Null(entry.LastModified);
        }

        [Theory]
        [InlineData(0L)]            // zero → null
        [InlineData(-1L)]           // negative → null
        [InlineData(long.MaxValue)] // overflows FromFileTimeUtc → null
        public void Parse_ReturnsNullLastModified_WhenFileTimeIsInvalid(long fileTime)
        {
            // Arrange
            var bytes = BuildEntryBytes("X", offset: 0u, size: 0u, fileTime: fileTime);
            using var parser = new PacArchiveEntryParser(new MemoryStream(bytes));

            // Act
            var entry = parser.Parse();

            // Assert
            Assert.Null(entry.LastModified);
        }

        [Fact]
        public void Parse_ReturnsParsedDateTime_WhenFileTimeIsValid()
        {
            // Arrange
            var expected = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var bytes = BuildEntryBytes("X", offset: 0u, size: 0u, fileTime: expected.ToFileTimeUtc());
            using var parser = new PacArchiveEntryParser(new MemoryStream(bytes));

            // Act
            var entry = parser.Parse();

            // Assert
            Assert.Equal(expected, entry.LastModified);
        }

        private static byte[] BuildEntryBytes(string fileName, uint offset, uint size, long fileTime)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(Encoding.ASCII.GetBytes(fileName));
            writer.Write((byte)0);          // null terminator
            writer.Write(new byte[4]);      // 4 padding nulls
            writer.Write(offset);
            writer.Write(size);
            writer.Write(fileTime);
            return ms.ToArray();
        }
    }
}
