using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    public class FinCollisionShapesTests
    {
        private const float EPSILON = 1e-4f;

        private static NiBoundingVolume ReadVolume(Func<FinStreamBuilder, FinStreamBuilder> writeVolume)
        {
            var builder = new FinStreamBuilder().Header().SizedString("NiNode").NiObject(0x10)
                .Byte(0).Floats(0, 0, 0).Floats(1, 0, 0, 0, 1, 0, 0, 0, 1).Floats(1).Floats(0, 0, 0)
                .Refs()
                .UInt32(0)
                .UInt32(1);
            var bytes = writeVolume(builder).NiNodeFields([], []).EndOfFile().ToArray();
            var node = Assert.IsType<NiNode>(Assert.Single(FinReader.Read("TEST", bytes).Objects));

            return Assert.IsAssignableFrom<NiBoundingVolume>(node.BoundingVolume);
        }

        private static FinStreamBuilder Box(FinStreamBuilder b, float[] axes, float[] extents)
        {
            return b.UInt32(1).Floats(1, 2, 3).Floats(axes).Floats(extents).Byte(0);
        }

        private static readonly float[] IDENTITY = [1, 0, 0, 0, 1, 0, 0, 0, 1];

        private static float[] Size(FinCollisionShape shape)
        {
            return shape.Shape["box"]!["size"]!.AsArray().Select(n => n!.GetValue<float>()).ToArray();
        }

        [Fact]
        public void FromVolume_WritesBox_WithFullSizeAndAxes()
        {
            // Arrange
            var volume = ReadVolume(b => Box(b, IDENTITY, [20, 20, 2]));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            var shape = Assert.Single(shapes);
            Assert.Equal(0, skipped);
            Assert.Equal("box", shape.Shape["type"]!.GetValue<string>());
            Assert.Equal([40f, 40f, 4f], Size(shape));
            Assert.Equal(new Vector3(1, 2, 3), shape.Translation);
            Assert.Equal(Quaternion.Identity, shape.Rotation);
        }

        [Fact]
        public void FromVolume_RotatesBox_WhenAxesAreTurned()
        {
            // Arrange
            var volume = ReadVolume(b => Box(b, [0, 1, 0, -1, 0, 0, 0, 0, 1], [1, 1, 1]));

            // Act
            var (shapes, _) = FinCollisionShapes.FromVolume(volume);

            // Assert
            var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2);
            var rotation = Assert.Single(shapes).Rotation;
            Assert.True(MathF.Abs(Quaternion.Dot(expected, rotation)) > 1 - EPSILON);
            var turned = Vector3.Transform(Vector3.UnitX, rotation);
            Assert.True(Vector3.Distance(turned, Vector3.UnitY) < EPSILON);
        }

        [Fact]
        public void FromVolume_FlipsThirdAxis_WhenAxesAreLeftHanded()
        {
            // Arrange
            var volume = ReadVolume(b => Box(b, [1, 0, 0, 0, 1, 0, 0, 0, -1], [2, 3, 4]));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            var shape = Assert.Single(shapes);
            Assert.Equal(0, skipped);
            Assert.Equal([4f, 6f, 8f], Size(shape));
            Assert.Equal(Quaternion.Identity, shape.Rotation);
        }

        [Fact]
        public void FromVolume_SkipsBox_WhenAxesAreNotOrthonormal()
        {
            // Arrange
            var volume = ReadVolume(b => Box(b, [1, 0, 0, 0.5f, 1, 0, 0, 0, 1], [1, 1, 1]));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            Assert.Empty(shapes);
            Assert.Equal(1, skipped);
        }

        [Fact]
        public void FromVolume_WritesSphere_AtCenter()
        {
            // Arrange
            var volume = ReadVolume(b => b.UInt32(0).Floats(1, 2, 3, 4).Byte(0));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            var shape = Assert.Single(shapes);
            Assert.Equal(0, skipped);
            Assert.Equal("sphere", shape.Shape["type"]!.GetValue<string>());
            Assert.Equal(4f, shape.Shape["sphere"]!["radius"]!.GetValue<float>());
            Assert.Equal(new Vector3(1, 2, 3), shape.Translation);
        }

        [Fact]
        public void FromVolume_WritesCapsule_AlongSegment()
        {
            // Arrange
            var volume = ReadVolume(b => b.UInt32(2).Floats(0, 0, 0, 0, 0, 10, 2).Byte(0));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            var shape = Assert.Single(shapes);
            Assert.Equal(0, skipped);
            Assert.Equal("capsule", shape.Shape["type"]!.GetValue<string>());
            Assert.Equal(2f, shape.Shape["capsule"]!["radius"]!.GetValue<float>());
            Assert.Equal(14f, shape.Shape["capsule"]!["height"]!.GetValue<float>());
            Assert.Equal(new Vector3(0, 0, 5), shape.Translation);
            Assert.True(Vector3.Distance(Vector3.Transform(Vector3.UnitY, shape.Rotation), Vector3.UnitZ) < EPSILON);
        }

        [Fact]
        public void FromVolume_MapsYOntoMinusY_WhenCapsuleRunsDownMinusY()
        {
            // Arrange
            var volume = ReadVolume(b => b.UInt32(2).Floats(0, 0, 0, 0, -10, 0, 2).Byte(0));

            // Act
            var (shapes, _) = FinCollisionShapes.FromVolume(volume);

            // Assert
            var shape = Assert.Single(shapes);
            Assert.True(Vector3.Distance(Vector3.Transform(Vector3.UnitY, shape.Rotation), -Vector3.UnitY) < EPSILON);
        }

        [Fact]
        public void FromVolume_WritesSphere_WhenCapsuleSegmentIsZero()
        {
            // Arrange
            var volume = ReadVolume(b => b.UInt32(2).Floats(1, 1, 1, 0, 0, 0, 2).Byte(0));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            var shape = Assert.Single(shapes);
            Assert.Equal(0, skipped);
            Assert.Equal("sphere", shape.Shape["type"]!.GetValue<string>());
            Assert.Equal(new Vector3(1, 1, 1), shape.Translation);
        }

        [Fact]
        public void FromVolume_FlattensUnion()
        {
            // Arrange
            var volume = ReadVolume(b => Box(b.UInt32(4).UInt32(2), IDENTITY, [1, 1, 1])
                .UInt32(4).UInt32(2)
                .UInt32(0).Floats(0, 0, 0, 1).Byte(0)
                .UInt32(2).Floats(0, 0, 0, 0, 0, 1, 1).Byte(0));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            Assert.Equal(3, shapes.Count);
            Assert.Equal(0, skipped);
        }

        [Fact]
        public void FromVolume_CountsSkipped_ForInvertedHalfSpaceAndIntersection()
        {
            // Arrange
            var volume = ReadVolume(b =>
            {
                b = b.UInt32(4).UInt32(4)
                    .UInt32(0).Floats(0, 0, 0, 1).Byte(1)       // inverted sphere
                    .UInt32(5).Floats(0, 0, 1, 2)               // half space
                    .UInt32(6).UInt32(2);                       // intersection
                b = Box(b, IDENTITY, [1, 1, 1]).UInt32(5).Floats(1, 0, 0, 0);

                return Box(b, IDENTITY, [1, 1, 1]);
            });

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            Assert.Single(shapes);
            Assert.Equal(3, skipped);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void FromVolume_SkipsSphere_WhenRadiusIsDegenerate(float radius)
        {
            // Arrange
            var volume = ReadVolume(b => b.UInt32(0).Floats(0, 0, 0, radius).Byte(0));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            Assert.Empty(shapes);
            Assert.Equal(1, skipped);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(float.NaN)]
        public void FromVolume_SkipsBox_WhenAnExtentIsDegenerate(float extent)
        {
            // Arrange
            var volume = ReadVolume(b => Box(b, IDENTITY, [1, extent, 1]));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            Assert.Empty(shapes);
            Assert.Equal(1, skipped);
        }

        [Fact]
        public void FromVolume_SkipsCapsule_WhenDirectionIsNotFinite()
        {
            // Arrange
            var volume = ReadVolume(b => b.UInt32(2).Floats(0, 0, 0, float.NaN, 0, 1, 1).Byte(0));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            Assert.Empty(shapes);
            Assert.Equal(1, skipped);
        }

        [Fact]
        public void FromVolume_SkipsLozenge()
        {
            // Arrange
            var volume = ReadVolume(b => b.UInt32(3).Floats(0, 0, 0, 1, 0, 0, 0, 1, 0, 1));

            // Act
            var (shapes, skipped) = FinCollisionShapes.FromVolume(volume);

            // Assert
            Assert.Empty(shapes);
            Assert.Equal(1, skipped);
        }

        [Theory]
        [InlineData(0u, true)]
        [InlineData(4u, true)]
        [InlineData(6u, true)]
        [InlineData(1u, false)]
        [InlineData(2u, false)]
        [InlineData(3u, false)]
        [InlineData(5u, false)]
        [InlineData(7u, false)]
        public void IsFinalShapeMode_IsTrueForZeroFourSix(uint mode, bool expected)
        {
            // Act
            var result = FinCollisionShapes.IsFinalShapeMode(mode);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
