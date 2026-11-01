using Malumware.BetaTeam.Lib.IO.Tga;

namespace Malumware.BetaTeam.Lib.Tests.IO.Tga
{
    public class TgaHeaderParserTests
    {
        [Fact]
        public void Parse_ReadsHeaderFields_WhenHeaderIsValid()
        {
            // Arrange
            var bytes = new TgaBuilder()
                .Header(imageType: 10, width: 64, height: 32, pixelDepth: 32, descriptor: 0x28, idLength: 3)
                .ToArray();
            using (var parser = new TgaHeaderParser(new MemoryStream(bytes)))
            {
                // Act
                var header = parser.Parse();

                // Assert
                Assert.Equal(3, header.IdLength);
                Assert.Equal(10, header.ImageType);
                Assert.Equal(64, header.Width);
                Assert.Equal(32, header.Height);
                Assert.Equal(32, header.PixelDepth);
                Assert.True(header.TopToBottom);
                Assert.True(header.IsRunLength);
                Assert.True(header.IsValid);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(9)]
        public void Parse_ThrowsInvalidData_WhenImageTypeIsColorMapped(byte imageType)
        {
            // Arrange
            var bytes = new TgaBuilder()
                .Header(imageType, width: 1, height: 1, pixelDepth: 8, colorMapType: 1)
                .ToArray();
            using (var parser = new TgaHeaderParser(new MemoryStream(bytes)))
            {
                // Act & Assert
                Assert.Throws<InvalidDataException>(() => parser.Parse());
            }
        }

        [Theory]
        [InlineData(2, 8)]
        [InlineData(3, 24)]
        [InlineData(10, 16)]
        public void Parse_ThrowsInvalidData_WhenDepthDoesNotFitType(byte imageType, byte pixelDepth)
        {
            // Arrange
            var bytes = new TgaBuilder()
                .Header(imageType, width: 1, height: 1, pixelDepth)
                .ToArray();
            using (var parser = new TgaHeaderParser(new MemoryStream(bytes)))
            {
                // Act & Assert
                Assert.Throws<InvalidDataException>(() => parser.Parse());
            }
        }
    }
}
