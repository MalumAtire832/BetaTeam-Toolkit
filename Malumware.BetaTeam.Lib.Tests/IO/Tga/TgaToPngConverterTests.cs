using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Malumware.BetaTeam.Lib.IO.Tga;

namespace Malumware.BetaTeam.Lib.Tests.IO.Tga
{
    public class TgaToPngConverterTests
    {
        private static readonly byte[] SIGNATURE = [137, 80, 78, 71, 13, 10, 26, 10];

        [Fact]
        public void ToPng_WritesSignatureAndRgbHeader_WhenImageHasNoAlpha()
        {
            // Arrange
            var image = new TgaImage(3, 2, false, new byte[3 * 2 * 4]);

            // Act
            var png = new TgaToPngConverter().ToPng(image);

            // Assert
            Assert.Equal(SIGNATURE, png[..8]);
            var header = Chunks(png).First();
            Assert.Equal("IHDR", header.Type);
            Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(header.Data));
            Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(header.Data.AsSpan(4)));
            Assert.Equal(8, header.Data[8]);  // bit depth
            Assert.Equal(2, header.Data[9]);  // colour type: RGB
        }

        [Fact]
        public void ToPng_UsesRgba_WhenImageHasAlpha()
        {
            // Arrange
            var image = new TgaImage(1, 1, true, [1, 2, 3, 4]);

            // Act
            var png = new TgaToPngConverter().ToPng(image);

            // Assert
            Assert.Equal(6, Chunks(png).First().Data[9]); // colour type: RGBA
        }

        [Fact]
        public void ToPng_StoresRowsUnfiltered_WhenDecompressed()
        {
            // Arrange: 2 x 2, one RGB and one RGBA case
            var pixels = new byte[] { 1, 2, 3, 255, 4, 5, 6, 255, 7, 8, 9, 255, 10, 11, 12, 255 };

            // Act
            var rgb = Inflate(new TgaToPngConverter().ToPng(new TgaImage(2, 2, false, pixels)));
            var rgba = Inflate(new TgaToPngConverter().ToPng(new TgaImage(2, 2, true, pixels)));

            // Assert
            Assert.Equal([0, 1, 2, 3, 4, 5, 6, 0, 7, 8, 9, 10, 11, 12], rgb);
            Assert.Equal([0, 1, 2, 3, 255, 4, 5, 6, 255, 0, 7, 8, 9, 255, 10, 11, 12, 255], rgba);
        }

        [Fact]
        public void ToPng_WritesValidCrcs()
        {
            // Arrange
            var image = new TgaImage(4, 4, true, Enumerable.Range(0, 64).Select(e => (byte)e).ToArray());

            // Act
            var chunks = Chunks(new TgaToPngConverter().ToPng(image)).ToList();

            // Assert
            Assert.Equal(["IHDR", "IDAT", "IEND"], chunks.Select(e => e.Type));
            foreach (var chunk in chunks)
            {
                Assert.Equal(BitwiseCrc(Encoding.ASCII.GetBytes(chunk.Type).Concat(chunk.Data).ToArray()), chunk.Crc);
            }
        }

        private static IEnumerable<(string Type, byte[] Data, uint Crc)> Chunks(byte[] png)
        {
            var position = SIGNATURE.Length;
            while (position < png.Length)
            {
                var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
                var type = Encoding.ASCII.GetString(png, position + 4, 4);
                var data = png[(position + 8)..(position + 8 + length)];
                var crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position + 8 + length));
                yield return (type, data, crc);
                position += 12 + length;
            }
        }

        private static byte[] Inflate(byte[] png)
        {
            var data = Chunks(png)
                .Where(e => e.Type == "IDAT")
                .SelectMany(e => e.Data)
                .ToArray();
            using (var input = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);

                return output.ToArray();
            }
        }

        // CRC-32 bit by bit, independent of the converter's table
        private static uint BitwiseCrc(byte[] bytes)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in bytes)
            {
                crc ^= b;
                for (var i = 0; i < 8; i++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                }
            }

            return ~crc;
        }
    }
}
