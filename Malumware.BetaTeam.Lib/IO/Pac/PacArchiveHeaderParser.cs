using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public class PacArchiveHeaderParser : BinaryParser<PacArchiveHeader>
    {
        public PacArchiveHeaderParser(Stream stream)
            : base(stream) { }

        public override PacArchiveHeader Parse()
        {
            var magic = Reader.ReadBytes(4);
            var size = Reader.ReadUInt32();
            Seek(4, SeekOrigin.Current); // Reserved
            var payloadOffset = Reader.ReadUInt32();
            var fileCount = Reader.ReadUInt32();

            var header = new PacArchiveHeader(magic, size, payloadOffset, fileCount);

            if (!header.IsValid)
            {
                throw new InvalidDataException(
                    "Not a PACK archive"
                );
            }

            return header;            
        }
    }
}