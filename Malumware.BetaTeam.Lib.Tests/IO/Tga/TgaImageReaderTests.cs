using Malumware.BetaTeam.Lib.IO.Tga;

namespace Malumware.BetaTeam.Lib.Tests.IO.Tga
{
    public class TgaImageReaderTests
    {
        [Fact]
        public void Read_ReturnsImage_WithCorrectHeaderAndPixelData()
        {
            // Arrange — 1×1 image, 24bpp → 3 bytes of pixel data
            var pixelData = new byte[] { 0xFF, 0x80, 0x00 };
            var bytes = BuildTgaBytes(
                idLength: 0,
                colorMapType: 0,
                imageType: TgaImageType.UncompressedTrueColor,
                width: 1,
                height: 1,
                pixelDepth: 24,
                imageId: [],
                colorMapData: null,
                pixelData
            );

            // Act
            var image = TgaImageReader.Read(() => new MemoryStream(bytes));

            // Assert
            Assert.Equal(TgaImageType.UncompressedTrueColor, image.Header.ImageType);
            Assert.Equal(0, image.Header.IdLength);
            Assert.Equal(1, image.Header.ImageSpec.Width);
            Assert.Equal(1, image.Header.ImageSpec.Height);
            Assert.Equal(24, image.Header.ImageSpec.PixelDepth);
            Assert.Empty(image.Data.ImageId);
            Assert.Null(image.Data.ColorMapData);
            Assert.Equal(pixelData, image.Data.PixelData);
        }

        [Fact]
        public void Read_ReturnsImageId_WhenIdLengthIsNonZero()
        {
            // Arrange
            var imageId = new byte[] { 0x41, 0x42, 0x43 };
            var pixelData = new byte[] { 0x7F };
            var bytes = BuildTgaBytes(
                idLength: 3,
                colorMapType: 0,
                imageType: TgaImageType.UncompressedGrayscale,
                width: 1,
                height: 1,
                pixelDepth: 8,
                imageId,
                colorMapData: null,
                pixelData
            );

            // Act
            var image = TgaImageReader.Read(() => new MemoryStream(bytes));

            // Assert
            Assert.Equal(3, image.Header.IdLength);
            Assert.Equal(imageId, image.Data.ImageId);
            Assert.Equal(pixelData, image.Data.PixelData);
        }

        private static byte[] BuildTgaBytes(
            byte idLength, byte colorMapType, TgaImageType imageType,
            ushort width, ushort height, byte pixelDepth,
            byte[] imageId, byte[]? colorMapData, byte[] pixelData,
            ushort colorMapLength = 0, byte colorMapEntrySize = 0)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // 18-byte fixed header
            writer.Write(idLength);
            writer.Write(colorMapType);
            writer.Write((byte)imageType);
            writer.Write((ushort)0);           // color map first entry index
            writer.Write(colorMapLength);
            writer.Write(colorMapEntrySize);
            writer.Write((ushort)0);           // x origin
            writer.Write((ushort)0);           // y origin
            writer.Write(width);
            writer.Write(height);
            writer.Write(pixelDepth);
            writer.Write((byte)0);             // image descriptor

            // Data sections
            writer.Write(imageId);
            if (colorMapData is not null)
            {
                writer.Write(colorMapData);                
            }
            writer.Write(pixelData);

            return stream.ToArray();
        }
    }
}
