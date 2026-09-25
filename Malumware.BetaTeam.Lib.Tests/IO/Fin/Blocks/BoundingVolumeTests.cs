using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Blocks
{
    public class BoundingVolumeTests
    {
        // A NiNode whose bounding volume is written by the caller after the has-bound flag
        private static NiNode ReadNode(Func<FinStreamBuilder, FinStreamBuilder> writeVolume)
        {
            var builder = new FinStreamBuilder().Header().SizedString("NiNode").NiObject(0x10)
                .Byte(0).Floats(0, 0, 0).Floats(1, 0, 0, 0, 1, 0, 0, 0, 1).Floats(1).Floats(0, 0, 0)
                .Refs()
                .UInt32(0)                                  // collision propagate
                .UInt32(1);                                 // has bounding volume
            var bytes = writeVolume(builder).NiNodeFields([], []).EndOfFile().ToArray();
            var file = FinReader.Read("TEST", bytes);
            return Assert.IsType<NiNode>(Assert.Single(file.Objects));
        }

        [Fact]
        public void Read_ReturnsSphere_WhenTypeIsZero()
        {
            // Act
            var node = ReadNode(b => b.UInt32(0).Floats(1, 2, 3, 4).Byte(1));

            // Assert
            var sphere = Assert.IsType<NiSphereBV>(node.BoundingVolume);
            Assert.Equal(new Vector3(1, 2, 3), sphere.Center);
            Assert.Equal(4f, sphere.Radius);
            Assert.True(sphere.Inverted);
        }

        [Fact]
        public void Read_ReturnsBox_WhenTypeIsOne()
        {
            // Act
            var node = ReadNode(b => b.UInt32(1)
                .Floats(1, 2, 3)                            // center
                .Floats(1, 0, 0, 0, 1, 0, 0, 0, 1)          // axes
                .Floats(4, 5, 6)                            // extents
                .Byte(0));                                  // inverted

            // Assert
            var box = Assert.IsType<NiBoxBV>(node.BoundingVolume);
            Assert.Equal(new Vector3(1, 2, 3), box.Center);
            Assert.Equal([Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ], box.Axes);
            Assert.Equal(new Vector3(4, 5, 6), box.Extents);
            Assert.False(box.Inverted);
        }

        [Fact]
        public void Read_ReturnsCapsule_WhenTypeIsTwo()
        {
            // Act
            var node = ReadNode(b => b.UInt32(2).Floats(1, 2, 3, 0, 0, 1, 0.5f).Byte(0));

            // Assert
            var capsule = Assert.IsType<NiCapsuleBV>(node.BoundingVolume);
            Assert.Equal(new Vector3(1, 2, 3), capsule.Origin);
            Assert.Equal(new Vector3(0, 0, 1), capsule.Direction);
            Assert.Equal(0.5f, capsule.Radius);
        }

        [Fact]
        public void Read_ReturnsLozenge_WhenTypeIsThree()
        {
            // Act
            var node = ReadNode(b => b.UInt32(3).Floats(1, 2, 3, 1, 0, 0, 0, 1, 0, 0.25f));

            // Assert
            var lozenge = Assert.IsType<NiLozengeBV>(node.BoundingVolume);
            Assert.Equal(new Vector3(1, 2, 3), lozenge.Origin);
            Assert.Equal(Vector3.UnitX, lozenge.Edge0);
            Assert.Equal(Vector3.UnitY, lozenge.Edge1);
            Assert.Equal(0.25f, lozenge.Radius);
        }

        [Fact]
        public void Read_ReturnsNestedVolumes_WhenTypeIsUnion()
        {
            // Act
            var node = ReadNode(b => b.UInt32(4)
                .UInt32(2)                                  // count
                .UInt32(0).Floats(0, 0, 0, 1).Byte(0)       // sphere
                .UInt32(5).Floats(0, 0, 1, 2));             // half space

            // Assert
            var union = Assert.IsType<NiUnionBV>(node.BoundingVolume);
            Assert.Equal(2, union.Volumes.Count);
            Assert.IsType<NiSphereBV>(union.Volumes[0]);
            var halfSpace = Assert.IsType<DDHalfSpaceBV>(union.Volumes[1]);
            Assert.Equal(new FinPlane(Vector3.UnitZ, 2), halfSpace.Plane);
        }

        [Fact]
        public void Read_ReturnsNestedVolumes_WhenTypeIsIntersection()
        {
            // Act
            var node = ReadNode(b => b.UInt32(6).UInt32(1).UInt32(5).Floats(1, 0, 0, 0));

            // Assert
            var intersection = Assert.IsType<DDIntersectionBV>(node.BoundingVolume);
            Assert.IsType<DDHalfSpaceBV>(Assert.Single(intersection.Volumes));
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenTypeIsUnknown()
        {
            // Act
            var act = () => ReadNode(b => b.UInt32(7));

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("type 7", exception.Message);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenVolumesNestTooDeep()
        {
            // Act: unions nested far beyond any real file, each holding one more union
            var act = () => ReadNode(b =>
            {
                for (var i = 0; i < NiBoundingVolume.MAX_DEPTH + 1; i++)
                {
                    b = b.UInt32(4).UInt32(1);
                }
                return b.UInt32(5).Floats(1, 0, 0, 0);
            });

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("nested", exception.Message);
        }
    }
}
