using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public sealed class PacArchiveEntryDataParser : IDisposable
    {
        private readonly BinaryReader _reader;

        public PacArchiveEntryDataParser(Stream stream)
        {
            _reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
        }

        public byte[] Parse(PacArchiveEntry entry)
        {
            _reader.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);
            return _reader.ReadBytes(entry.Size);
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
