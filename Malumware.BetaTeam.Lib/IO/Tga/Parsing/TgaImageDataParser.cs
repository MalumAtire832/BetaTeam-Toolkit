using Malumware.BetaTeam.Lib.IO.Parsers;
using Malumware.BetaTeam.Lib.IO.Tga.Models;

namespace Malumware.BetaTeam.Lib.IO.Tga.Parsing
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

            var bytes = _header.ColorMapSpec.ColorMapLength * BitsToBytes(_header.ColorMapSpec.EntrySize);
            return Reader.ReadBytes(bytes);
        }

        private byte[] ParsePixelData()
        {
            var totalPixels = _header.ImageSpec.Width * _header.ImageSpec.Height;
            var bytesPerPixel = BitsToBytes(_header.ImageSpec.PixelDepth);

            return _header.ImageType switch
            {
                TgaImageType.RleTrueColor => ParsePixelDataRle(totalPixels, bytesPerPixel),
                TgaImageType.RleColorMapped => ParsePixelDataRle(totalPixels, bytesPerPixel),
                TgaImageType.RleGrayscale => ParsePixelDataRle(totalPixels, bytesPerPixel),
                _ => Reader.ReadBytes(totalPixels * bytesPerPixel),
            };
        }

        /// <summary>
        ///     RLE types store compressed packets of variable length rather than a flat array, so they need a
        ///     different read path. The result is always fully decompressed pixel bytes, keeping
        ///     <c>TgaImageData.PixelData</c> format-agnostic for consumers.
        /// </summary>
        private byte[] ParsePixelDataRle(int totalPixels, int bytesPerPixel)
        {
            var output = new byte[totalPixels * bytesPerPixel];
            var written = 0;

            // Each packet starts with a header byte. Bit 7 selects the packet type;
            // bits 6–0 encode the pixel count minus one.
            while (written < output.Length)
            {
                var packetHeader = Reader.ReadByte();
                var count = (packetHeader & 0x7F) + 1; // bits 6–0: pixel count minus one (range 1–128)

                if ((packetHeader & 0x80) != 0) // bit 7: set = run-length packet, clear = raw packet
                {
                    // Run-length packet: one pixel follows and is repeated `count` times
                    var pixel = Reader.ReadBytes(bytesPerPixel);
                    for (var i = 0; i < count; i++)
                    {
                        pixel.CopyTo(output, written);
                        written += bytesPerPixel;
                    }
                }
                else
                {
                    // Raw packet: `count` pixels follow and are copied literally
                    var byteCount = count * bytesPerPixel;
                    Reader.ReadBytes(byteCount).CopyTo(output, written);
                    written += byteCount;
                }
            }

            return output;
        }

        /// <summary>
        ///     Converts a bit count to the number of bytes required to hold it.
        /// </summary>
        /// 
        /// <remarks>
        ///     Uses ceiling division so sub-byte-boundary widths round up rather than truncate:
        ///         15 bits → 2 bytes, 24 bits → 3 bytes, 32 bits → 4 bytes.
        /// </remarks>
        /// 
        private static int BitsToBytes(int bits) => (bits + 7) / 8;
    }
}