using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Tga
{
    // Writes a decoded TGA as a PNG: RGBA when the TGA had alpha, RGB otherwise, 8 bits per channel, rows unfiltered
    public class TgaToPngConverter
    {
        private const byte BIT_DEPTH = 8;
        private const byte COLOR_TYPE_RGB = 2;
        private const byte COLOR_TYPE_RGBA = 6;
        private const byte FILTER_NONE = 0;
        private const uint CRC_POLYNOMIAL = 0xEDB88320;

        private static readonly byte[] SIGNATURE = [137, 80, 78, 71, 13, 10, 26, 10];
        private static readonly uint[] CRC_TABLE = BuildCrcTable();

        public byte[] ToPng(TgaImage image)
        {
            using (var output = new MemoryStream())
            {
                output.Write(SIGNATURE);
                WriteChunk(output, "IHDR", BuildHeader(image));
                WriteChunk(output, "IDAT", Compress(image));
                WriteChunk(output, "IEND", []);

                return output.ToArray();
            }
        }

        public void Convert(TgaImage image, string outputPath)
        {
            File.WriteAllBytes(outputPath, ToPng(image));
        }

        private static byte[] BuildHeader(TgaImage image)
        {
            var header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
            header[8] = BIT_DEPTH;
            header[9] = image.HasAlpha ? COLOR_TYPE_RGBA : COLOR_TYPE_RGB;
            // Compression, filter and interlace methods: all 0, the only standard ones

            return header;
        }

        private static byte[] Compress(TgaImage image)
        {
            var channels = image.HasAlpha ? 4 : 3;
            using (var output = new MemoryStream())
            {
                using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
                {
                    var row = new byte[1 + image.Width * channels];
                    row[0] = FILTER_NONE;
                    for (var y = 0; y < image.Height; y++)
                    {
                        for (var x = 0; x < image.Width; x++)
                        {
                            var source = (y * image.Width + x) * 4;
                            Array.Copy(image.Pixels, source, row, 1 + x * channels, channels);
                        }
                        zlib.Write(row);
                    }
                }

                return output.ToArray();
            }
        }

        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            var number = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
            output.Write(number);
            output.Write(typeBytes);
            output.Write(data);
            BinaryPrimitives.WriteUInt32BigEndian(number, Crc(typeBytes, data));
            output.Write(number);
        }

        private static uint Crc(byte[] type, byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in type.Concat(data))
            {
                crc = CRC_TABLE[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }

            return ~crc;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (var n = 0u; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? CRC_POLYNOMIAL ^ (c >> 1) : c >> 1;
                }
                table[n] = c;
            }

            return table;
        }
    }
}
