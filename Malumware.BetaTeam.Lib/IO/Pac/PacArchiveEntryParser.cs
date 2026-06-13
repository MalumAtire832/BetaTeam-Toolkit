using System.Text;
using Malumware.BetaTeam.Lib.IO.Parsers;
using Malumware.Common;

namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public class PacArchiveEntryParser : BinaryParser<PacArchiveEntry>
    {
        public PacArchiveEntryParser(Stream stream)
            : base(stream) { }

        public override PacArchiveEntry Parse()
        {
            var fileName = ReadName();
            Seek(4, SeekOrigin.Current); // Skip 4 padding nulls
            var fileDataOffset = Reader.ReadUInt32();
            var fileSize = Reader.ReadUInt32();
            var fileModifyTime = ReadModifyTime();

            return new PacArchiveEntry(
                fileName, fileDataOffset, (int)fileSize, fileModifyTime
            );
        }

        private string ReadName()
        {
            var bytes = Reader.ReadBytesUntil(0, 20);
            return Encoding.ASCII.GetString(bytes);
        }

        private DateTime? ReadModifyTime()
        {
            var raw = Reader.ReadInt64();

            if (raw <= 0)
            {
                return null;
            }

            try
            {
                var parsed = DateTime.FromFileTimeUtc(raw);
                return parsed;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }
}