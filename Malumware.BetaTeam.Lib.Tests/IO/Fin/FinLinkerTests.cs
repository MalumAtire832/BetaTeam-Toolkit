using Malumware.BetaTeam.Lib.IO.Fin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinLinkerTests
    {
        private static FinFile Read(byte[] bytes)
        {
            return FinReader.Read("TEST", bytes, TestRegistry.Create());
        }

        [Fact]
        public void Read_ResolvesReference_WhenTargetExists()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10).UInt32(7)
                .TopLevel().SizedString("TestParentBlock").NiObject(0x20).UInt32(0x10)
                .EndOfFile()
                .ToArray();

            // Act
            var file = Read(bytes);

            // Assert
            var parent = Assert.IsType<TestParentBlock>(file.TopLevelObjects[0]);
            Assert.Same(file.Objects[0], parent.Child.Target);
        }

        [Fact]
        public void Read_ResolvesForwardReference_WhenTargetComesLater()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .TopLevel().SizedString("TestParentBlock").NiObject(0x20).UInt32(0x10)
                .SizedString("TestBlock").NiObject(0x10).UInt32(7)
                .EndOfFile()
                .ToArray();

            // Act
            var file = Read(bytes);

            // Assert
            var parent = Assert.IsType<TestParentBlock>(file.TopLevelObjects[0]);
            Assert.Same(file.Objects[1], parent.Child.Target);
        }

        [Fact]
        public void Read_LeavesTargetNull_WhenLinkIdIsZero()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestParentBlock").NiObject(0x20).UInt32(0)
                .EndOfFile()
                .ToArray();

            // Act
            var file = Read(bytes);

            // Assert
            var parent = Assert.IsType<TestParentBlock>(file.Objects[0]);
            Assert.True(parent.Child.IsNull);
            Assert.Null(parent.Child.Target);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenReferenceIsDangling()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestParentBlock").NiObject(0x20).UInt32(0x99)
                .EndOfFile()
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("0x00000099", exception.Message);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenTargetHasWrongType()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestParentBlock").NiObject(0x10).UInt32(0)
                .SizedString("TestParentBlock").NiObject(0x20).UInt32(0x10)
                .EndOfFile()
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("TestBlock", exception.Message);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenLinkIdIsDuplicated()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("TestBlock").NiObject(0x10).UInt32(1)
                .SizedString("TestBlock").NiObject(0x10).UInt32(2)
                .EndOfFile()
                .ToArray();

            // Act
            var act = () => Read(bytes);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("Duplicate", exception.Message);
        }
    }
}
