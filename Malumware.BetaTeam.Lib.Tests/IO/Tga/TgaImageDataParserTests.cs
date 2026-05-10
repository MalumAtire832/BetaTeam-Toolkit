using Malumware.BetaTeam.Lib.IO.Tga;

namespace Malumware.BetaTeam.Lib.Tests.IO.Tga
{
    public class TgaImageDataParserTests
    {
        [Fact]
        public void Parse_ReturnsPixelData_ForTrueColorImage()
        {
            // Arrange — 2×2 image, 24bpp → 12 bytes of pixel data
            var pixelData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            var header = BuildHeader(idLength: 0, colorMapType: 0, width: 2, height: 2, pixelDepth: 24);
            var bytes = BuildBuffer(header, imageId: [], colorMapData: null, pixelData);
            using var parser = new TgaImageDataParser(new MemoryStream(bytes), header);

            // Act
            var data = parser.Parse();

            // Assert
            Assert.Empty(data.ImageId);
            Assert.Null(data.ColorMapData);
            Assert.Equal(pixelData, data.PixelData);
        }

        [Fact]
        public void Parse_ReturnsImageId_WhenIdLengthIsNonZero()
        {
            // Arrange
            var imageId = new byte[] { 0xAA, 0xBB, 0xCC };
            var pixelData = new byte[] { 0xFF, 0x80, 0x00, 0xFF };
            var header = BuildHeader(idLength: 3, colorMapType: 0, width: 1, height: 1, pixelDepth: 32);
            var bytes = BuildBuffer(header, imageId, colorMapData: null, pixelData);
            using var parser = new TgaImageDataParser(new MemoryStream(bytes), header);

            // Act
            var data = parser.Parse();

            // Assert
            Assert.Equal(imageId, data.ImageId);
            Assert.Equal(pixelData, data.PixelData);
        }

        [Fact]
        public void Parse_ReturnsColorMapData_WhenColorMapTypeIsOne()
        {
            // Arrange — 2 color map entries × 24bpp = 6 bytes of color map data
            var colorMapData = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66 };
            var pixelData = new byte[] { 0x01 };
            var header = BuildHeader(
                idLength: 0, colorMapType: 1, width: 1, height: 1, pixelDepth: 8,
                colorMapLength: 2, colorMapEntrySize: 24
            );
            var bytes = BuildBuffer(header, imageId: [], colorMapData, pixelData);
            using var parser = new TgaImageDataParser(new MemoryStream(bytes), header);

            // Act
            var data = parser.Parse();

            // Assert
            Assert.Equal(colorMapData, data.ColorMapData);
            Assert.Equal(pixelData, data.PixelData);
        }

        [Fact]
        public void Parse_ReturnsNullColorMapData_WhenColorMapTypeIsZero()
        {
            // Arrange
            var pixelData = new byte[] { 0xFF };
            var header = BuildHeader(idLength: 0, colorMapType: 0, width: 1, height: 1, pixelDepth: 8);
            var bytes = BuildBuffer(header, imageId: [], colorMapData: null, pixelData);
            using var parser = new TgaImageDataParser(new MemoryStream(bytes), header);

            // Act
            var data = parser.Parse();

            // Assert
            Assert.Null(data.ColorMapData);
        }

        private static TgaImageHeader BuildHeader(
            byte idLength, byte colorMapType, ushort width, ushort height, byte pixelDepth,
            ushort colorMapLength = 0, byte colorMapEntrySize = 0)
        {
            return new TgaImageHeader(
                idLength,
                colorMapType,
                TgaImageType.UncompressedTrueColor,
                new TgaColorMapSpec(firstEntryIndex: 0, colorMapLength, colorMapEntrySize),
                new TgaImageSpec(xOrigin: 0, yOrigin: 0, width, height, pixelDepth, imageDescriptor: 0)
            );
        }

        private static byte[] BuildBuffer(TgaImageHeader header, byte[] imageId, byte[]? colorMapData, byte[] pixelData)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // 18-byte fixed header
            writer.Write(header.IdLength);
            writer.Write(header.ColorMapType);
            writer.Write((byte)header.ImageType);
            writer.Write(header.ColorMapSpec.FirstEntryIndex);
            writer.Write(header.ColorMapSpec.ColorMapLength);
            writer.Write(header.ColorMapSpec.EntrySize);
            writer.Write(header.ImageSpec.XOrigin);
            writer.Write(header.ImageSpec.YOrigin);
            writer.Write(header.ImageSpec.Width);
            writer.Write(header.ImageSpec.Height);
            writer.Write(header.ImageSpec.PixelDepth);
            writer.Write(header.ImageSpec.ImageDescriptor);

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
