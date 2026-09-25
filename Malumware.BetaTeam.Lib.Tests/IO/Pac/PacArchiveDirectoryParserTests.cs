using Malumware.BetaTeam.Lib.IO.Pac;

namespace Malumware.BetaTeam.Lib.Tests.IO.Pac
{
    public class PacArchiveDirectoryParserTests
    {
        [Fact]
        public void Parse_ReturnsMetadata_FromDirectoryEntryBuffer()
        {
            // Arrange
            var bytes = BuildDirectoryBytes(writer =>
            {
                writer.Write(1u);         // file count
                PacTestData.WriteEntry(writer, "FOO", offsetLow: 0x44u, size: 3u);
                writer.Write(0u);         // sub-directory count
            });
            using var parser = new PacArchiveDirectoryParser(new MemoryStream(bytes));

            // Act
            var entries = parser.Parse();

            // Assert
            var entry = Assert.Single(entries);
            Assert.Equal("FOO", entry.FileName);
            Assert.Equal(0x44L, entry.Offset);
            Assert.Equal(3, entry.Size);
            Assert.Null(entry.LastModified);
        }

        [Fact]
        public void Parse_CombinesOffsetHighAndLow()
        {
            // Arrange
            var bytes = BuildDirectoryBytes(writer =>
            {
                writer.Write(1u);
                PacTestData.WriteEntry(writer, "BIG", offsetLow: 0x10u, size: 1u, offsetHigh: 0x2u);
                writer.Write(0u);
            });
            using var parser = new PacArchiveDirectoryParser(new MemoryStream(bytes));

            // Act
            var entry = Assert.Single(parser.Parse());

            // Assert
            Assert.Equal(0x2_0000_0010L, entry.Offset);
        }

        [Theory]
        [InlineData(0L)]            // zero → null
        [InlineData(-1L)]           // negative → null
        [InlineData(long.MaxValue)] // overflows FromFileTimeUtc → null
        public void Parse_ReturnsNullLastModified_WhenFileTimeIsInvalid(long fileTime)
        {
            // Arrange
            var bytes = BuildDirectoryBytes(writer =>
            {
                writer.Write(1u);
                PacTestData.WriteEntry(writer, "X", offsetLow: 0u, size: 0u, fileTime: fileTime);
                writer.Write(0u);
            });
            using var parser = new PacArchiveDirectoryParser(new MemoryStream(bytes));

            // Act
            var entry = Assert.Single(parser.Parse());

            // Assert
            Assert.Null(entry.LastModified);
        }

        [Fact]
        public void Parse_ReturnsParsedDateTime_WhenFileTimeIsValid()
        {
            // Arrange
            var expected = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var bytes = BuildDirectoryBytes(writer =>
            {
                writer.Write(1u);
                PacTestData.WriteEntry(writer, "X", offsetLow: 0u, size: 0u, fileTime: expected.ToFileTimeUtc());
                writer.Write(0u);
            });
            using var parser = new PacArchiveDirectoryParser(new MemoryStream(bytes));

            // Act
            var entry = Assert.Single(parser.Parse());

            // Assert
            Assert.Equal(expected, entry.LastModified);
        }

        [Fact]
        public void Parse_ReturnsEmptyList_WhenDirectoryIsEmpty()
        {
            // Arrange
            var bytes = BuildDirectoryBytes(writer =>
            {
                writer.Write(0u);         // file count
                writer.Write(0u);         // sub-directory count
            });
            var stream = new MemoryStream(bytes);
            using var parser = new PacArchiveDirectoryParser(stream);

            // Act
            var entries = parser.Parse();

            // Assert
            Assert.Empty(entries);
            Assert.Equal(bytes.Length, stream.Position);
        }

        [Fact]
        public void Parse_PrefixesFileNames_WithSubDirectoryPath()
        {
            // Arrange
            var bytes = BuildDirectoryBytes(writer =>
            {
                writer.Write(1u);
                PacTestData.WriteEntry(writer, "ROOT.TXT", offsetLow: 0u, size: 0u);
                writer.Write(1u);         // one sub-directory
                PacTestData.WriteName(writer, "SUB");
                writer.Write(1u);
                PacTestData.WriteEntry(writer, "A.TXT", offsetLow: 0u, size: 0u);
                writer.Write(1u);         // nested sub-directory
                PacTestData.WriteName(writer, "DEEP");
                writer.Write(1u);
                PacTestData.WriteEntry(writer, "B.TXT", offsetLow: 0u, size: 0u);
                writer.Write(0u);
            });
            var stream = new MemoryStream(bytes);
            using var parser = new PacArchiveDirectoryParser(stream);

            // Act
            var entries = parser.Parse();

            // Assert
            Assert.Equal(
                ["ROOT.TXT", @"SUB\A.TXT", @"SUB\DEEP\B.TXT"],
                entries.Select(e => e.FileName)
            );
            Assert.Equal(bytes.Length, stream.Position);
        }

        [Fact]
        public void Parse_ThrowsInvalidDataException_WhenNameIsTooLong()
        {
            // Arrange
            var name = new string('A', PacArchiveDirectoryParser.MAX_NAME_LENGTH + 1);
            var bytes = BuildDirectoryBytes(writer =>
            {
                writer.Write(1u);
                PacTestData.WriteEntry(writer, name, offsetLow: 0u, size: 0u);
                writer.Write(0u);
            });
            using var parser = new PacArchiveDirectoryParser(new MemoryStream(bytes));

            // Act
            var act = () => parser.Parse();

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        [Theory]
        [InlineData("")]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData(@"..\EVIL.TXT")]
        [InlineData("../EVIL.TXT")]
        [InlineData("C:EVIL.TXT")]
        public void Parse_ThrowsInvalidDataException_WhenFileNameIsUnsafe(string name)
        {
            // Arrange
            var bytes = BuildDirectoryBytes(writer =>
            {
                writer.Write(1u);
                PacTestData.WriteEntry(writer, name, offsetLow: 0u, size: 0u);
                writer.Write(0u);
            });
            using var parser = new PacArchiveDirectoryParser(new MemoryStream(bytes));

            // Act
            var act = () => parser.Parse();

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        [Theory]
        [InlineData("..")]
        [InlineData("SUB/..")]
        public void Parse_ThrowsInvalidDataException_WhenDirectoryNameIsUnsafe(string name)
        {
            // Arrange
            var bytes = BuildDirectoryBytes(writer =>
            {
                writer.Write(0u);
                writer.Write(1u);
                PacTestData.WriteName(writer, name);
                writer.Write(0u);
                writer.Write(0u);
            });
            using var parser = new PacArchiveDirectoryParser(new MemoryStream(bytes));

            // Act
            var act = () => parser.Parse();

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        private static byte[] BuildDirectoryBytes(Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            write(writer);
            return stream.ToArray();
        }
    }
}
