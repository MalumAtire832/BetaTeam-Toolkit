using System.Buffers.Binary;
using Malumware.BetaTeam.Lib.IO.Tga.Models;

namespace Malumware.BetaTeam.Lib.IO.Tga.Conversion
{
    internal class ColorMappedTgaConverter
    {
        /// <summary>Converts a colo-mapped TGA image to PNG raw row format.</summary>
        internal byte[] ToPng(TgaImage image)
        {
            var imageSpec = image.Header.ImageSpec;
            var colorMapSpec = image.Header.ColorMapSpec;
            return ConvertToPng(
                imageSpec.Width,
                imageSpec.Height,
                imageSpec.IsTopToBottom(),
                image.Data.PixelData,
                colorMapSpec.EntrySize,
                colorMapSpec.FirstEntryIndex,
                image.Data.ColorMapData ?? throw new InvalidDataException("Color-mapped TGA has no color map data.")
            );
        }

        /// <summary>
        ///     Converts colo-mapped TGA pixel data to PNG raw row format: flips row order if needed,
        ///     prefixes each row with a PNG filter byte, and resolves each pixel index to its colo
        ///     map entry before writing out as RGB(A).
        /// </summary>
        private static byte[] ConvertToPng(
            int width, int height, bool isTopToBottom,
            byte[] pixelData,
            int entrySize, int firstEntryIndex, byte[] colorMap)
        {
            var bytesPerEntry = entrySize <= 16 ? 2 : entrySize / 8;
            var hasAlpha = entrySize == 32;
            var bytesPerPixelOut = hasAlpha ? 4 : 3;

            // Stride is an image term for "how many bytes separate the start of one row from the start of the next."
            // It's called stride rather than width because in some formats rows have padding bytes at the end.
            var stride = 1 + width * bytesPerPixelOut;
            var result = new byte[height * stride];

            // 15/16-bit entries: xRRRRRGGGGGBBBBB stored little-endian.
            // Each 5-bit component must be scaled up to an 8-bit (0–255) value.
            const int redShift    = 10;   // bits 14–10
            const int greenShift  = 5;    // bits 9–5
            const int fiveBitMask = 0x1F; // 0b0001_1111 — masks a single 5-bit component
            static byte Scale5To8(int component) => (byte)(component * 255 / 31);

            // r = row counter
            for (var r = 0; r < height; r++)
            {
                var srcRow = isTopToBottom ? r : height - 1 - r;
                var dstBase = r * stride;

                result[dstBase] = 0x00; // filter byte: None

                // p = pixel in a row counter
                for (var p = 0; p < width; p++)
                {
                    var index = pixelData[srcRow * width + p] - firstEntryIndex;
                    var mapOffset = index * bytesPerEntry;
                    var dst = dstBase + 1 + p * bytesPerPixelOut;

                    if (entrySize <= 16)
                    {
                        var entry = BinaryPrimitives.ReadUInt16LittleEndian(colorMap.AsSpan(mapOffset));
                        result[dst]     = Scale5To8((entry >> redShift)   & fiveBitMask); // R
                        result[dst + 1] = Scale5To8((entry >> greenShift) & fiveBitMask); // G
                        result[dst + 2] = Scale5To8(entry & fiveBitMask);                 // B
                    }
                    else
                    {
                        // 24/32-bit: BGR(A) → RGB(A)
                        result[dst]     = colorMap[mapOffset + 2]; // R
                        result[dst + 1] = colorMap[mapOffset + 1]; // G
                        result[dst + 2] = colorMap[mapOffset];     // B
                        if (hasAlpha)
                        {
                            result[dst + 3] = colorMap[mapOffset + 3]; // A
                        }
                    }
                }
            }

            return result;
        }
    }
}
