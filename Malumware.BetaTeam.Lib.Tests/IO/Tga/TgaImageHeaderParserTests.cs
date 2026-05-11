using Malumware.BetaTeam.Lib.IO.Tga;
using Malumware.BetaTeam.Lib.IO.Tga.Models;
using Malumware.BetaTeam.Lib.IO.Tga.Parsing;

namespace Malumware.BetaTeam.Lib.Tests.IO.Tga
{
    public class TgaImageHeaderParserTests
    {
        [Fact]
        public void Parse_ReturnsHeader_WithAllFieldsCorrect()
        {
            // Arrange
            var bytes = BuildHeaderBytes(
                idLength: 5,
                colorMapType: 0,
                imageType: TgaImageType.UncompressedTrueColor,
                firstEntryIndex: 0,
                colorMapLength: 0,
                colorMapEntrySize: 0,
                xOrigin: 10,
                yOrigin: 20,
                width: 320,
                height: 240,
                pixelDepth: 24,
                imageDescriptor: 0
            );
            var stream = new MemoryStream(bytes);
            using var parser = new TgaImageHeaderParser(stream);

            // Act
            var header = parser.Parse();

            // Assert
            Assert.Equal(5, header.IdLength);
            Assert.Equal(0, header.ColorMapType);
            Assert.Equal(TgaImageType.UncompressedTrueColor, header.ImageType);
            Assert.Equal(0, header.ColorMapSpec.FirstEntryIndex);
            Assert.Equal(0, header.ColorMapSpec.ColorMapLength);
            Assert.Equal(0, header.ColorMapSpec.EntrySize);
            Assert.Equal(10, header.ImageSpec.XOrigin);
            Assert.Equal(20, header.ImageSpec.YOrigin);
            Assert.Equal(320, header.ImageSpec.Width);
            Assert.Equal(240, header.ImageSpec.Height);
            Assert.Equal(24, header.ImageSpec.PixelDepth);
            Assert.Equal(0, header.ImageSpec.ImageDescriptor);
        }

        [Fact]
        public void Parse_ReturnsColorMapSpec_WithCorrectFields()
        {
            // Arrange
            var bytes = BuildHeaderBytes(
                idLength: 0,
                colorMapType: 1,
                imageType: TgaImageType.UncompressedColorMapped,
                firstEntryIndex: 4,
                colorMapLength: 256,
                colorMapEntrySize: 24,
                xOrigin: 0,
                yOrigin: 0,
                width: 64,
                height: 64,
                pixelDepth: 8,
                imageDescriptor: 0
            );
            var stream = new MemoryStream(bytes);
            using var parser = new TgaImageHeaderParser(stream);

            // Act
            var header = parser.Parse();

            // Assert
            Assert.Equal(1, header.ColorMapType);
            Assert.Equal(TgaImageType.UncompressedColorMapped, header.ImageType);
            Assert.Equal(4, header.ColorMapSpec.FirstEntryIndex);
            Assert.Equal(256, header.ColorMapSpec.ColorMapLength);
            Assert.Equal(24, header.ColorMapSpec.EntrySize);
        }

        private static byte[] BuildHeaderBytes(
            byte idLength, byte colorMapType, TgaImageType imageType,
            ushort firstEntryIndex, ushort colorMapLength, byte colorMapEntrySize,
            ushort xOrigin, ushort yOrigin, ushort width, ushort height,
            byte pixelDepth, byte imageDescriptor)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(idLength);
            writer.Write(colorMapType);
            writer.Write((byte)imageType);
            writer.Write(firstEntryIndex);
            writer.Write(colorMapLength);
            writer.Write(colorMapEntrySize);
            writer.Write(xOrigin);
            writer.Write(yOrigin);
            writer.Write(width);
            writer.Write(height);
            writer.Write(pixelDepth);
            writer.Write(imageDescriptor);
            return stream.ToArray();
        }
    }
}
