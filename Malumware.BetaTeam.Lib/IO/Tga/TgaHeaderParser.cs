using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Tga
{
    public class TgaHeaderParser : AbstractParser<TgaHeader>
    {
        public TgaHeaderParser(Stream stream)
            : base(stream) { }

        public override TgaHeader Parse()
        {
            if (Reader.BaseStream.Length - Reader.BaseStream.Position < TgaHeader.SIZE)
            {
                throw new InvalidDataException("A TGA file needs an 18-byte header");
            }

            var idLength = Reader.ReadByte();
            var colorMapType = Reader.ReadByte();
            var imageType = Reader.ReadByte();
            Reader.ReadUInt16(); // first colour map entry
            var colorMapLength = Reader.ReadUInt16();
            var colorMapEntrySize = Reader.ReadByte();
            Reader.ReadUInt16(); // x origin
            Reader.ReadUInt16(); // y origin
            var width = Reader.ReadUInt16();
            var height = Reader.ReadUInt16();
            var pixelDepth = Reader.ReadByte();
            var descriptor = Reader.ReadByte();

            var header = new TgaHeader(idLength, colorMapType, imageType, colorMapLength, colorMapEntrySize, width,
                height, pixelDepth, descriptor);

            if (!header.IsValid)
            {
                throw new InvalidDataException(
                    $"Unsupported TGA image: type {imageType}, {pixelDepth} bits per pixel, colour map type {colorMapType}"
                );
            }

            return header;
        }
    }
}
