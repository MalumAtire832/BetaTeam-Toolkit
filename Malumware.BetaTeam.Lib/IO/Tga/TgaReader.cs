namespace Malumware.BetaTeam.Lib.IO.Tga
{
    // Decodes TGA files the way the game's engine does: a 32-bit image always uses its fourth byte as alpha, whatever
    // the header's alpha-bit count says, and every other depth is opaque. Greyscale becomes equal red, green and blue.
    public class TgaReader
    {
        private const int RUN_FLAG = 0x80;

        public TgaImage Read(string filePath)
        {
            return Read(File.ReadAllBytes(filePath));
        }

        public TgaImage Read(byte[] bytes)
        {
            return Read(bytes, out _);
        }

        // consumed is the number of bytes the image used, so tests can check nothing is left over
        internal static TgaImage Read(byte[] bytes, out int consumed)
        {
            TgaHeader header;
            using (var parser = new TgaHeaderParser(new MemoryStream(bytes)))
            {
                header = parser.Parse();
            }

            var position = TgaHeader.SIZE + header.IdLength;
            if (header.ColorMapType == 1)
            {
                // A colour map in a true-colour image is allowed but unused
                position += (header.ColorMapLength * header.ColorMapEntrySize + 7) / 8;
            }

            var width = header.Width;
            var height = header.Height;
            var pixelSize = header.PixelDepth / 8;
            var pixels = new byte[width * height * 4];
            var count = width * height;
            var index = 0;
            while (index < count)
            {
                var repeat = 1;
                var raw = 1;
                if (header.IsRunLength)
                {
                    var packet = ReadByte(bytes, ref position);
                    var length = (packet & ~RUN_FLAG) + 1;
                    if ((packet & RUN_FLAG) != 0)
                    {
                        repeat = length;
                    }
                    else
                    {
                        raw = length;
                    }
                    if (index + repeat * raw > count)
                    {
                        throw new InvalidDataException($"A run-length packet passes the image's last pixel at {index}");
                    }
                }

                for (var i = 0; i < raw; i++)
                {
                    if (position + pixelSize > bytes.Length)
                    {
                        throw new InvalidDataException($"Pixel data ends after {index} of {count} pixels");
                    }
                    for (var j = 0; j < repeat; j++)
                    {
                        WritePixel(header, bytes, position, pixels, Target(header, index));
                        index++;
                    }
                    position += pixelSize;
                }
            }

            consumed = position;

            return new TgaImage(width, height, header.PixelDepth == 32, pixels);
        }

        private static byte ReadByte(byte[] bytes, ref int position)
        {
            if (position >= bytes.Length)
            {
                throw new InvalidDataException("Pixel data ends inside a run-length packet");
            }

            return bytes[position++];
        }

        // Where the index-th stored pixel goes in the top-to-bottom output
        private static int Target(TgaHeader header, int index)
        {
            var row = index / header.Width;
            var column = index % header.Width;
            var outputRow = header.TopToBottom ? row : header.Height - 1 - row;

            return (outputRow * header.Width + column) * 4;
        }

        private static void WritePixel(TgaHeader header, byte[] source, int position, byte[] pixels, int target)
        {
            if (header.IsGreyscale)
            {
                pixels[target] = source[position];
                pixels[target + 1] = source[position];
                pixels[target + 2] = source[position];
                pixels[target + 3] = 255;

                return;
            }

            // Stored blue, green, red (, alpha)
            pixels[target] = source[position + 2];
            pixels[target + 1] = source[position + 1];
            pixels[target + 2] = source[position];
            pixels[target + 3] = header.PixelDepth == 32 ? source[position + 3] : (byte)255;
        }
    }
}
