using Malumware.BetaTeam.Lib.IO.Pac;

namespace Malumware.BetaTeam.Lib.Tests.IO.Pac
{
    public class PacArchiveReaderTests
    {
        [Fact]
        public void Read_ReturnsArchive_WithSingleEntryAndData()
        {
            // Arrange
            var payload = new byte[] { 0x42, 0x41, 0x52 };
            var bytes = BuildArchiveBytes(("FOO", payload));
            var reader = new PacArchiveReader();

            // Act
            var archive = reader.Read("test.pac", () => new MemoryStream(bytes));

            // Assert
            Assert.Equal("test", archive.FileName);
            Assert.Single(archive.Entries);

            var (entry, data) = archive.Entries.First();
            Assert.Equal("FOO", entry.FileName);
            Assert.Equal(payload.Length, entry.Size);
            Assert.Equal(payload, data);
        }

        [Fact]
        public void Read_ReturnsEmptyEntries_WhenDirectoryIsEmpty()
        {
            // Arrange
            var bytes = new byte[]
            {
                (byte)'P', (byte)'A', (byte)'C', (byte)'K', // magic
                0x18, 0x00, 0x00, 0x00,                     // archive size = 0x18
                0x00, 0x00, 0x00, 0x00,                     // unknown
                0x18, 0x00, 0x00, 0x00,                     // directory size = 0x18
                0x00, 0x00, 0x00, 0x00,                     // file count = 0
                0x00, 0x00, 0x00, 0x00,                     // sub-directory count = 0
            };
            var reader = new PacArchiveReader();

            // Act
            var archive = reader.Read("empty.pac", () => new MemoryStream(bytes));

            // Assert
            Assert.Empty(archive.Entries);
        }

        [Fact]
        public void Read_SkipsEntryReferencingTheArchiveItself()
        {
            // Arrange
            var payload = new byte[] { 0x01 };
            var bytes = BuildArchiveBytes(("SELF.PAC", []), ("FOO", payload));
            var reader = new PacArchiveReader();

            // Act
            var archive = reader.Read("Self.pac", () => new MemoryStream(bytes));

            // Assert
            var (entry, data) = Assert.Single(archive.Entries);
            Assert.Equal("FOO", entry.FileName);
            Assert.Equal(payload, data);
        }

        private static byte[] BuildArchiveBytes(params (string Name, byte[] Data)[] files)
        {
            var directorySize = (uint)(PacArchiveHeader.SIZE + 4 + files.Sum(f => PacTestData.EntrySize(f.Name)) + 4);
            var archiveSize = directorySize + (uint)files.Sum(f => f.Data.Length);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                PacTestData.WriteHeader(writer, archiveSize, directorySize);

                // Directory
                writer.Write((uint)files.Length);
                var offset = directorySize;
                foreach (var (name, data) in files)
                {
                    PacTestData.WriteEntry(writer, name, offset, (uint)data.Length);
                    offset += (uint)data.Length;
                }
                writer.Write(0u);             // sub-directory count

                // File data
                foreach (var (_, data) in files)
                {
                    writer.Write(data);
                }

                return ms.ToArray();
            }
        }
    }
}
