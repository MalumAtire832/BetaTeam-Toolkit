using Malumware.BetaTeam.Lib.IO.Tga.Models;

namespace Malumware.BetaTeam.Lib.IO.Tga.Conversion
{
    internal class DirectTgaConverter
    {
        /// <summary>Converts a direct-color TGA image to PNG raw row format.</summary>
        internal byte[] ToPng(TgaImage image)
        {
            var imageSpec = image.Header.ImageSpec;
            return ConvertToPng(
                imageSpec.Width,
                imageSpec.Height,
                imageSpec.IsTopToBottom(),
                image.Header.ImageType,
                imageSpec.PixelDepth,
                image.Data.PixelData
            );
        }

        private static byte[] ConvertToPng(
            int width, int height, bool isTopToBottom,
            TgaImageType imageType, int pixelDepth,
            byte[] pixelData)
        {
            return (imageType.IsGrayscale(), imageType.IsTrueColor(), pixelDepth) switch
            {
                (true, _, 8)  => ConvertGrayscaleToPng(width, height, isTopToBottom, pixelData),
                (_, true, 24) => ConvertTrueColorToPng(width, height, isTopToBottom, pixelData),
                (_, true, 32) => ConvertTrueColorWithAlphaToPng(width, height, isTopToBottom, pixelData),
                _ => throw new NotSupportedException(
                    $"TGA image type {imageType} with pixel depth {pixelDepth} is not supported."
                )
            };
        }

        /// <summary>
        ///     Converts 8-bit grayscale TGA pixel data to PNG raw row format: flips row order if needed,
        ///     and prefixes each row with a PNG filter byte.
        /// </summary>
        private static byte[] ConvertGrayscaleToPng(int width, int height, bool isTopToBottom, byte[] pixelData)
        {
            // Stride is an image term for "how many bytes separate the start of one row from the start of the next."
            // It's called stride rather than width because in some formats rows have padding bytes at the end.
            var stride = width + 1;
            var result = new byte[height * stride];

            // r = row counter
            for (var r = 0; r < height; r++)
            {
                var srcRow = isTopToBottom ? r : height - 1 - r;
                var dstBase = r * stride;

                result[dstBase] = 0x00; // filter byte: None

                // p = pixel in a row counter
                for (var p = 0; p < width; p++)
                {
                    result[dstBase + 1 + p] = pixelData[srcRow * width + p];
                }
            }

            return result;
        }

        /// <summary>
        ///     Converts 24-bit true-color TGA pixel data to PNG raw row format: flips row order if needed,
        ///     prefixes each row with a PNG filter byte, and swaps BGR to RGB.
        /// </summary>
        private static byte[] ConvertTrueColorToPng(int width, int height, bool isTopToBottom, byte[] pixelData)
        {
            const int bytesPerPixel = 3;

            // Stride is an image term for "how many bytes separate the start of one row from the start of the next."
            // It's called stride rather than width because in some formats rows have padding bytes at the end.
            var stride = width * bytesPerPixel + 1;
            var result = new byte[height * stride];

            // r = row counter
            for (var r = 0; r < height; r++)
            {
                var srcRow = isTopToBottom ? r : height - 1 - r;
                var srcBase = srcRow * width * bytesPerPixel;
                var dstBase = r * stride;

                result[dstBase] = 0x00; // filter byte: None

                // p = pixel in a row counter
                for (var p = 0; p < width; p++)
                {
                    var src = srcBase + p * bytesPerPixel;
                    var dst = dstBase + 1 + p * bytesPerPixel;

                    // TGA is BGR, PNG expects RGB: swap byte 0 (B) and byte 2 (R)
                    result[dst] = pixelData[src + 2]; // R
                    result[dst + 1] = pixelData[src + 1]; // G
                    result[dst + 2] = pixelData[src]; // B
                }
            }

            return result;
        }

        /// <summary>
        ///     Converts 32-bit true-color TGA pixel data to PNG raw row format: flips row order if needed,
        ///     prefixes each row with a PNG filter byte, and swaps BGRA to RGBA.
        /// </summary>
        private static byte[] ConvertTrueColorWithAlphaToPng(int width, int height, bool isTopToBottom, byte[] pixelData)
        {
            const int bytesPerPixel = 4;

            // Stride is an image term for "how many bytes separate the start of one row from the start of the next."
            // It's called stride rather than width because in some formats rows have padding bytes at the end.
            var stride = width * bytesPerPixel + 1;
            var result = new byte[height * stride];

            // r = row counter
            for (var r = 0; r < height; r++)
            {
                var srcRow = isTopToBottom ? r : height - 1 - r;
                var srcBase = srcRow * width * bytesPerPixel;
                var dstBase = r * stride;

                result[dstBase] = 0x00; // filter byte: None

                // p = pixel in a row counter
                for (var p = 0; p < width; p++)
                {
                    var src = srcBase + p * bytesPerPixel;
                    var dst = dstBase + 1 + p * bytesPerPixel;

                    // TGA is BGRA, PNG expects RGBA: swap byte 0 (B) and byte 2 (R)
                    result[dst] = pixelData[src + 2]; // R
                    result[dst + 1] = pixelData[src + 1]; // G
                    result[dst + 2] = pixelData[src]; // B
                    result[dst + 3] = pixelData[src + 3]; // A
                }
            }

            return result;
        }
    }
}