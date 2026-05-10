using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Tga
{
    public class TgaImageDataParser : AbstractParser<TgaImageData>
    {
        private readonly TgaImageHeader _header;

        public TgaImageDataParser(Stream stream, TgaImageHeader header) 
            : base(stream)
        {
            _header = header;
        }

        public override TgaImageData Parse()
        {
            Seek(TgaImageHeader.SIZE, SeekOrigin.Begin);

            var imageId = _header.IdLength > 0
                ? Reader.ReadBytes(_header.IdLength)
                : [];

            var colorMapData = ParseColorMapData();
            var pixelData = ParsePixelData();

            return new TgaImageData(imageId, colorMapData, pixelData);
        }

        private byte[]? ParseColorMapData()
        {
            // ColorMapType 1 means a color map is present; all other values mean none
            if (_header.ColorMapType != 1)
            {
                return null;
            }

            // EntrySize is in bits — divide by 8 to get bytes per entry
            // +7 ensures ceiling division: 15-bit entries (2 bytes) would truncate to 1 without it
            var bytes = _header.ColorMapSpec.ColorMapLength * ((_header.ColorMapSpec.EntrySize + 7) / 8);
            return Reader.ReadBytes(bytes);
        }

        private byte[] ParsePixelData()
        {
            // Total pixels = width × height; PixelDepth is in bits, so divide by 8 for bytes per pixel
            // +7 ensures ceiling division: 15-bit depth (2 bytes) would truncate to 1 without it
            var dimensions = _header.ImageSpec.Width * _header.ImageSpec.Height;
            var size = dimensions * ((_header.ImageSpec.PixelDepth + 7) / 8);
            return Reader.ReadBytes(size);
        }
    }
}
