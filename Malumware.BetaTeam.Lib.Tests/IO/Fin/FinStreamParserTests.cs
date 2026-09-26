using Malumware.BetaTeam.Lib.IO.Fin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinStreamParserTests
    {
        private static FinFile Read(byte[] bytes)
        {
            return FinReader.Read("TEST", bytes, TestRegistry.Create());
        }

        [Fact]
        public void Read_ReturnsBlocksInFileOrder_WhenStreamIsValid()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10, "First").UInt32(1)
                .TopLevel().SizedString("TestBlock").NiObject(0x20, "Second").UInt32(2)
                .EndOfFile()
                .ToArray();

            // Act
            var file = Read(bytes);

            // Assert
            Assert.Equal("TEST", file.Name);
            Assert.Equal(23, file.Header.Version);
            Assert.Equal(2, file.Objects.Count);
            var first = Assert.IsType<TestBlock>(file.Objects[0]);
            Assert.Equal("TestBlock", first.ClassName);
            Assert.Equal(0x10u, first.LinkId);
            Assert.Equal("First", first.Name);
            Assert.Equal(1u, first.Value);
            Assert.Equal(11, first.Offset);
            Assert.Same(file.Objects[1], Assert.Single(file.TopLevelObjects));
        }

        [Fact]
        public void Read_ThrowsUnknownClass_WithClassNameAndOffset_WhenClassIsNotRegistered()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10).UInt32(1)
                .SizedString("NiMystery").NiObject(0x20)
                .EndOfFile()
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.True(FinUnknownClass.TryGet(exception, out var className, out var offset));
            Assert.Equal("NiMystery", className);
            Assert.Equal(11 + 4 + 9 + 12 + 4, offset);    // header, class name, NiObject, value
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenBytesFollowEndOfFile()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header().EndOfFile().Byte(0).ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("1 bytes", exception.Message);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WithOffset_WhenFileEndsInsideBlock()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10)
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.False(FinUnknownClass.TryGet(exception, out _, out _));
            Assert.Contains("TestBlock", exception.Message);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenEndOfFileMarkerIsMissing()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10).UInt32(1)
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }
    }
}
