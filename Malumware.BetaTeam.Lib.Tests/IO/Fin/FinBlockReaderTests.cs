using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    public class FinBlockReaderTests
    {
        private static FinBlockReader CreateReader(byte[] bytes)
        {
            return new FinBlockReader(new BinaryReader(new MemoryStream(bytes)), TestRegistry.Create());
        }

        [Fact]
        public void ReadCString_ReturnsNull_WhenLengthIsZero()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().CString(null).ToArray());

            // Act
            var value = reader.ReadCString();

            // Assert
            Assert.Null(value);
            Assert.Equal(4, reader.Position);
        }

        [Fact]
        public void ReadCString_ReturnsText_WhenLengthIsPositive()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().CString("Box01").ToArray());

            // Act
            var value = reader.ReadCString();

            // Assert
            Assert.Equal("Box01", value);
        }

        [Fact]
        public void ReadSizedString_ThrowsInvalidDataException_WhenLengthExceedsMaximum()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().UInt32(0x0A96AB80).ToArray());

            // Act
            var act = () => reader.ReadSizedString(64);

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        [Fact]
        public void ReadCount_ThrowsInvalidDataException_WhenCountExceedsRemainingBytes()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().UInt32(1_000_000).UInt32(0).ToArray());

            // Act
            Action act = () => reader.ReadCount(4);

            // Assert
            Assert.Throws<InvalidDataException>(act);
        }

        [Fact]
        public void ReadMatrix3_ReadsNineFloatsInFileOrder()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().Floats(1, 2, 3, 4, 5, 6, 7, 8, 9).ToArray());

            // Act
            var matrix = reader.ReadMatrix3();

            // Assert
            Assert.Equal(new FinMatrix3(1, 2, 3, 4, 5, 6, 7, 8, 9), matrix);
        }

        [Fact]
        public void ReadVector3_ReadsThreeFloats()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().Floats(1, 2, 3).ToArray());

            // Act
            var vector = reader.ReadVector3();

            // Assert
            Assert.Equal(new Vector3(1, 2, 3), vector);
        }

        [Fact]
        public void ReadRefList_ReturnsUnresolvedRefs_WithNullForZero()
        {
            // Arrange
            var reader = CreateReader(new FinStreamBuilder().Refs(0x10, 0).ToArray());

            // Act
            var refs = reader.ReadRefList<TestBlock>();

            // Assert
            Assert.Equal(2, refs.Count);
            Assert.Equal(0x10u, refs[0].LinkId);
            Assert.True(refs[1].IsNull);
            Assert.Equal(2, reader.Refs.Count);
        }

        [Fact]
        public void ReadExtraDataList_ReadsRegisteredExtraData()
        {
            // Arrange
            var bytes = new FinStreamBuilder().UInt32(1).CString("TestExtraData").UInt32(42).ToArray();
            var reader = CreateReader(bytes);

            // Act
            var extraData = reader.ReadExtraDataList();

            // Assert
            var single = Assert.IsType<TestExtraData>(Assert.Single(extraData));
            Assert.Equal(42u, single.Value);
            Assert.Equal("TestExtraData", single.ClassName);
        }

        [Fact]
        public void ReadExtraDataList_ThrowsUnknownClass_WhenExtraDataIsNotRegistered()
        {
            // Arrange
            var bytes = new FinStreamBuilder().UInt32(1).CString("NiStringExtraData").UInt32(0).ToArray();
            var reader = CreateReader(bytes);

            // Act
            var act = () => reader.ReadExtraDataList();

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.True(FinUnknownClass.TryGet(exception, out var className, out var offset));
            Assert.Equal("NiStringExtraData", className);
            Assert.Equal(4, offset);
        }

        [Fact]
        public void ReadExtraDataList_UsesNiExtraData_WhenClassNameIsNull()
        {
            // Arrange
            var bytes = new FinStreamBuilder().UInt32(1).CString(null).ToArray();
            var reader = CreateReader(bytes);

            // Act
            var act = () => reader.ReadExtraDataList();

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.True(FinUnknownClass.TryGet(exception, out var className, out _));
            Assert.Equal("NiExtraData", className);
        }
    }
}
