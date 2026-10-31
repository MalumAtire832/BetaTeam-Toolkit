using Malumware.BetaTeam.Lib.IO.Tga;

namespace Malumware.BetaTeam.Lib.Tests.IO.Tga
{
    public class TgaReaderTests
    {
        [Fact]
        public void Read_FlipsRows_WhenOriginIsBottomLeft()
        {
            // Arrange: a 1 x 2 image, the bottom row (blue) stored first
            var bytes = new TgaBuilder()
                .Header(imageType: 2, width: 1, height: 2, pixelDepth: 24)
                .Bytes(255, 0, 0)
                .Bytes(0, 0, 255)
                .ToArray();

            // Act
            var image = new TgaReader().Read(bytes);

            // Assert: top row (red) first
            Assert.Equal([255, 0, 0, 255, 0, 0, 255, 255], image.Pixels);
        }

        [Fact]
        public void Read_KeepsRowOrder_WhenOriginIsTopLeft()
        {
            // Arrange
            var bytes = new TgaBuilder()
                .Header(imageType: 2, width: 1, height: 2, pixelDepth: 24, descriptor: TgaBuilder.TOP_TO_BOTTOM)
                .Bytes(0, 0, 255)
                .Bytes(255, 0, 0)
                .ToArray();

            // Act
            var image = new TgaReader().Read(bytes);

            // Assert
            Assert.Equal([255, 0, 0, 255, 0, 0, 255, 255], image.Pixels);
        }

        [Fact]
        public void Read_ConvertsBgrToRgbWithOpaqueAlpha_When24Bit()
        {
            // Arrange
            var bytes = new TgaBuilder()
                .Header(imageType: 2, width: 1, height: 1, pixelDepth: 24)
                .Bytes(10, 20, 30)
                .ToArray();

            // Act
            var image = new TgaReader().Read(bytes);

            // Assert
            Assert.False(image.HasAlpha);
            Assert.Equal(1, image.Width);
            Assert.Equal(1, image.Height);
            Assert.Equal([30, 20, 10, 255], image.Pixels);
        }

        [Fact]
        public void Read_KeepsFourthByteAsAlpha_When32BitAndAlphaBitsAreZero()
        {
            // Arrange: descriptor alpha bits 0, which the engine ignores
            var bytes = new TgaBuilder()
                .Header(imageType: 2, width: 1, height: 1, pixelDepth: 32, descriptor: 0)
                .Bytes(10, 20, 30, 40)
                .ToArray();

            // Act
            var image = new TgaReader().Read(bytes);

            // Assert
            Assert.True(image.HasAlpha);
            Assert.Equal([30, 20, 10, 40], image.Pixels);
        }

        [Fact]
        public void Read_RepeatsGreyIntoRgb_WhenGreyscale()
        {
            // Arrange
            var bytes = new TgaBuilder()
                .Header(imageType: 3, width: 2, height: 1, pixelDepth: 8, descriptor: 8)
                .Bytes(7, 200)
                .ToArray();

            // Act
            var image = new TgaReader().Read(bytes);

            // Assert
            Assert.False(image.HasAlpha);
            Assert.Equal([7, 7, 7, 255, 200, 200, 200, 255], image.Pixels);
        }

        [Fact]
        public void Read_DecodesRunsAcrossRows_WhenRunLengthEncoded()
        {
            // Arrange: 2 x 2, top to bottom; a run of 3 crosses into the second row, then one raw pixel
            var bytes = new TgaBuilder()
                .Header(imageType: 10, width: 2, height: 2, pixelDepth: 32, descriptor: TgaBuilder.TOP_TO_BOTTOM | 8)
                .Run(3, 1, 2, 3, 4)
                .Raw(1, 5, 6, 7, 8)
                .ToArray();

            // Act
            var image = new TgaReader().Read(bytes);

            // Assert
            Assert.Equal([3, 2, 1, 4, 3, 2, 1, 4, 3, 2, 1, 4, 7, 6, 5, 8], image.Pixels);
        }

        [Fact]
        public void Read_SkipsImageId_WhenHeaderHasOne()
        {
            // Arrange
            var bytes = new TgaBuilder()
                .Header(imageType: 3, width: 1, height: 1, pixelDepth: 8, idLength: 4)
                .Bytes(9)
                .ToArray();

            // Act
            var image = new TgaReader().Read(bytes);

            // Assert
            Assert.Equal([9, 9, 9, 255], image.Pixels);
        }

        [Fact]
        public void Read_ThrowsInvalidData_WhenPixelDataIsTruncated()
        {
            // Arrange
            var bytes = new TgaBuilder()
                .Header(imageType: 2, width: 2, height: 2, pixelDepth: 24)
                .Bytes(1, 2, 3)
                .ToArray();

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => new TgaReader().Read(bytes));
        }

        [Fact]
        public void Read_ThrowsInvalidData_WhenRunPassesLastPixel()
        {
            // Arrange
            var bytes = new TgaBuilder()
                .Header(imageType: 11, width: 1, height: 1, pixelDepth: 8)
                .Run(2, 5)
                .ToArray();

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => new TgaReader().Read(bytes));
        }
    }
}
