using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Tga
{
    public class TgaImageHeaderParser : AbstractParser<TgaImageHeader>
    {
        public TgaImageHeaderParser(Stream stream)
            : base(stream) { }

        public override TgaImageHeader Parse()
        {
            var idLength = Reader.ReadByte();
            var colorMapType = Reader.ReadByte();
            var imageType = (TgaImageType)Reader.ReadByte();

            var colorMapSpec = new TgaColorMapSpec(
                firstEntryIndex: Reader.ReadUInt16(),
                colorMapLength: Reader.ReadUInt16(),
                entrySize: Reader.ReadByte()
            );

            var imageSpec = new TgaImageSpec(
                xOrigin: Reader.ReadUInt16(),
                yOrigin: Reader.ReadUInt16(),
                width: Reader.ReadUInt16(),
                height: Reader.ReadUInt16(),
                pixelDepth: Reader.ReadByte(),
                imageDescriptor: Reader.ReadByte()
            );

            return new TgaImageHeader(
                idLength, colorMapType, imageType, 
                colorMapSpec, 
                imageSpec
            );
        }
    }
}
