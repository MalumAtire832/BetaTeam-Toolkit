using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    public class FinGltfTransformTests
    {
        private const float TOLERANCE = 1e-5f;

        private static NiNode ReadNode(float[] translation, float[] rotation, float scale)
        {
            var bytes = new FinStreamBuilder().Header()
                .SizedString("NiNode").NiObject(0x10)
                .Byte(0)
                .Floats(translation)
                .Floats(rotation)
                .Floats(scale)
                .Floats(0, 0, 0)            // velocity
                .Refs()
                .UInt32(0)
                .UInt32(0)
                .NiNodeFields([], [])
                .EndOfFile()
                .ToArray();
            return Assert.IsType<NiNode>(Assert.Single(FinReader.Read("TEST", bytes).Objects));
        }

        private static void AssertClose(Vector3 expected, Vector3 actual)
        {
            Assert.True(Vector3.Distance(expected, actual) < TOLERANCE, $"Expected {expected}, got {actual}");
        }

        [Fact]
        public void ToLocalTransform_ReturnsIdentity_WhenTransformIsIdentity()
        {
            // Arrange
            var node = ReadNode([0, 0, 0], [1, 0, 0, 0, 1, 0, 0, 0, 1], 1);

            // Act
            var transform = FinGltfTransform.ToLocalTransform(node);

            // Assert
            Assert.True(transform.IsSRT);
            Assert.Equal(Quaternion.Identity, transform.Rotation);
            Assert.Equal(Vector3.One, transform.Scale);
            Assert.Equal(Vector3.Zero, transform.Translation);
        }

        // The stored groups are the rows of R, which rotates a point as R·v: this R turns +X into +Y
        [Fact]
        public void ToLocalTransform_ReadsGroupsAsRows_WhenRotationIsNotSymmetric()
        {
            // Arrange
            var node = ReadNode([0, 0, 0], [0, -1, 0, 1, 0, 0, 0, 0, 1], 1);

            // Act
            var transform = FinGltfTransform.ToLocalTransform(node);

            // Assert
            AssertClose(Vector3.UnitY, Vector3.Transform(Vector3.UnitX, transform.Rotation));
            AssertClose(-Vector3.UnitX, Vector3.Transform(Vector3.UnitY, transform.Rotation));
        }

        [Fact]
        public void ToLocalTransform_AppliesScaleRotationThenTranslation()
        {
            // Arrange
            var node = ReadNode([10, 20, 30], [0, -1, 0, 1, 0, 0, 0, 0, 1], 2);

            // Act
            var transform = FinGltfTransform.ToLocalTransform(node);

            // Assert: t + s·R·v, with R·(1, 0, 0) = (0, 1, 0)
            AssertClose(new Vector3(10, 22, 30), Vector3.Transform(Vector3.UnitX, transform.Matrix));
        }

        [Fact]
        public void ToLocalTransform_UsesNegativeScale_WhenRotationIsMirrored()
        {
            // Arrange
            var node = ReadNode([0, 0, 0], [0, -1, 0, 1, 0, 0, 0, 0, -1], 2);

            // Act
            var transform = FinGltfTransform.ToLocalTransform(node);

            // Assert
            Assert.True(transform.IsSRT);
            Assert.Equal(new Vector3(2, 2, -2), transform.Scale);
            AssertClose(new Vector3(0, 2, 0), Vector3.Transform(Vector3.UnitX, transform.Matrix));
            AssertClose(new Vector3(0, 0, -2), Vector3.Transform(Vector3.UnitZ, transform.Matrix));
        }

        [Fact]
        public void ToLocalTransform_UsesMatrix_WhenRotationIsSkewed()
        {
            // Arrange
            var node = ReadNode([1, 2, 3], [1, 0.5f, 0, 0, 1, 0, 0, 0, 1], 1);

            // Act
            var transform = FinGltfTransform.ToLocalTransform(node);

            // Assert: R·(0, 1, 0) is R's second column, (0.5, 1, 0)
            Assert.True(transform.IsMatrix);
            AssertClose(new Vector3(1.5f, 3, 3), Vector3.Transform(Vector3.UnitY, transform.Matrix));
        }

        [Fact]
        public void ToLocalTransform_ThrowsInvalidData_WhenTransformIsNotFinite()
        {
            // Arrange
            var node = ReadNode([float.NaN, 0, 0], [1, 0, 0, 0, 1, 0, 0, 0, 1], 1);

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => FinGltfTransform.ToLocalTransform(node));
        }

        [Fact]
        public void ToLocalTransform_ThrowsInvalidData_WhenRotationIsSingular()
        {
            // Arrange
            var node = ReadNode([0, 0, 0], [1, 0, 0, 1, 0, 0, 0, 0, 1], 1);

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => FinGltfTransform.ToLocalTransform(node));
        }

        [Fact]
        public void ZUpToYUp_MapsUpAndFrontToGltfAxes()
        {
            // Act
            var up = Vector3.Transform(Vector3.UnitZ, FinGltfTransform.ZUpToYUp);
            var front = Vector3.Transform(-Vector3.UnitY, FinGltfTransform.ZUpToYUp);

            // Assert
            AssertClose(Vector3.UnitY, up);
            AssertClose(Vector3.UnitZ, front);
        }
    }
}
