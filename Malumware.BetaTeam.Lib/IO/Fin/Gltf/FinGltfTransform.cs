using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using SharpGLTF.Transforms;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    internal static class FinGltfTransform
    {
        // How far RᵀR may be from the identity for R to count as a rotation; exported matrices are off by float noise
        private const float ORTHONORMAL_TOLERANCE = 1e-3f;

        // FIN files are Z-up like 3ds Max, glTF is Y-up. Turning the whole scene -90° about X maps +Z to +Y and the
        // 3ds Max front (-Y) to the glTF front (+Z), so the file's own values stay untouched below this.
        public static Quaternion ZUpToYUp { get; } = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2);

        public static AffineTransform ToLocalTransform(NiAVObject block)
        {
            var translation = block.Translation;
            var scale = block.Scale;
            var rotation = ToRowVectorMatrix(block.Rotation);

            if (!IsFinite(translation) || !float.IsFinite(scale) || !IsFinite(rotation))
            {
                throw new InvalidDataException($"{FinToGltfConverter.Describe(block)} has a transform that isn't a finite number");
            }

            if (IsOrthonormal(rotation))
            {
                if (rotation.GetDeterminant() > 0)
                {
                    return new AffineTransform(new Vector3(scale), ToQuaternion(rotation), translation);
                }

                // A mirrored rotation is a proper rotation after a flip along one axis, which glTF expresses as a
                // negative scale. In row-vector form the flip comes first, so undo it on the matrix's third row.
                var unmirrored = rotation with { M31 = -rotation.M31, M32 = -rotation.M32, M33 = -rotation.M33 };

                return new AffineTransform(new Vector3(scale, scale, -scale), ToQuaternion(unmirrored), translation);
            }

            // Not a rotation (skewed or scaled per axis), so no quaternion can express it; glTF takes it as a matrix
            var matrix = Matrix4x4.CreateScale(scale) * rotation * Matrix4x4.CreateTranslation(translation);
            if (MathF.Abs(matrix.GetDeterminant()) < 1e-12f)
            {
                throw new InvalidDataException($"{FinToGltfConverter.Describe(block)} has a rotation matrix that collapses it to a plane");
            }

            return new AffineTransform(matrix);
        }

        // NetImmerse rotates a point as R·v with the groups of the stored matrix as the rows of R (inferred from the
        // engine family: NiMatrix3 keeps m_pEntry[row][column] and NiAVObject combines transforms as parent·child,
        // not yet confirmed in this game's code). System.Numerics works with row vectors, v·M, so M = Rᵀ.
        internal static Matrix4x4 ToRowVectorMatrix(FinMatrix3 m)
        {
            return new Matrix4x4(
                m.M11, m.M21, m.M31, 0,
                m.M12, m.M22, m.M32, 0,
                m.M13, m.M23, m.M33, 0,
                0, 0, 0, 1);
        }

        private static Quaternion ToQuaternion(Matrix4x4 rotation)
        {
            return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotation));
        }

        private static bool IsOrthonormal(Matrix4x4 m)
        {
            var product = m * Matrix4x4.Transpose(m);
            var identity = Matrix4x4.Identity;
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    if (MathF.Abs(product[row, column] - identity[row, column]) > ORTHONORMAL_TOLERANCE)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }

        private static bool IsFinite(Matrix4x4 m)
        {
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    if (!float.IsFinite(m[row, column]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
