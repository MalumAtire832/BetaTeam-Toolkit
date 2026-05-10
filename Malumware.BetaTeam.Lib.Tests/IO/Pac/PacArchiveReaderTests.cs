using System.Linq;
using System.Text;
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
            var bytes = BuildArchiveBytes(fileName: "FOO", fileData: payload);
            var reader = new PacArchiveReader();

            // Act
            var archive = reader.Read("test.pac", () => new MemoryStream(bytes));

            // Assert
            Assert.Equal("test.pac", archive.FileName);
            Assert.Equal(1u, archive.Header.FileCount);
            Assert.Single(archive.Entries);

            var (entry, data) = archive.Entries.First();
            Assert.Equal("FOO", entry.FileName);
            Assert.Equal(payload.Length, entry.Size);
            Assert.Equal(payload, data);
        }

        [Fact]
        public void Read_ReturnsEmptyEntries_WhenFileCountIsZero()
        {
            // Arrange
            var bytes = new byte[]
            {
                (byte)'P', (byte)'A', (byte)'C', (byte)'K', // magic
                0x14, 0x00, 0x00, 0x00,                     // archive size = 0x14 (header only)
                0x00, 0x00, 0x00, 0x00,                     // reserved
                0x14, 0x00, 0x00, 0x00,                     // payload offset = 0x14
                0x00, 0x00, 0x00, 0x00,                     // file count = 0
            };
            var reader = new PacArchiveReader();

            // Act
            var archive = reader.Read("empty.pac", () => new MemoryStream(bytes));

            // Assert
            Assert.Empty(archive.Entries);
        }

        private static byte[] BuildArchiveBytes(string fileName, byte[] fileData)
        {
            var nameBytes = Encoding.ASCII.GetBytes(fileName);
            var entryRecordSize = nameBytes.Length + 1 + 4 + 4 + 4 + 8;
            var payloadOffset = (uint)(PacArchiveHeader.SIZE + entryRecordSize);
            var archiveSize = payloadOffset + (uint)fileData.Length;

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            // Header
            writer.Write(Encoding.ASCII.GetBytes("PACK"));
            writer.Write(archiveSize);
            writer.Write(0u);             // reserved
            writer.Write(payloadOffset);
            writer.Write(1u);             // file count

            // Directory entry
            writer.Write(nameBytes);
            writer.Write((byte)0);        // null terminator
            writer.Write(new byte[4]);    // padding
            writer.Write(payloadOffset);  // file's absolute offset
            writer.Write((uint)fileData.Length);
            writer.Write(0L);             // FILETIME

            // File data
            writer.Write(fileData);

            return ms.ToArray();
        }
    }
}
