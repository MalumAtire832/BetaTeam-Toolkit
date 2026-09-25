using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public class PacArchiveHeaderParser : AbstractParser<PacArchiveHeader>
    {
        public PacArchiveHeaderParser(Stream stream)
            : base(stream) { }

        public override PacArchiveHeader Parse()
        {
            var magic = Reader.ReadBytes(4);
            var size = Reader.ReadUInt32();
            Seek(4, SeekOrigin.Current); // Unknown, always 0 and ignored by the game
            var directorySize = Reader.ReadUInt32();

            var header = new PacArchiveHeader(magic, size, directorySize);

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