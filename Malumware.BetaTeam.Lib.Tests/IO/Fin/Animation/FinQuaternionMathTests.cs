using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Animation
{
    public class FinQuaternionMathTests
    {
        private const float TOLERANCE = 1e-6f;

        private static void AssertClose(Quaternion expected, Quaternion actual)
        {
            Assert.Equal(expected.X, actual.X, TOLERANCE);
            Assert.Equal(expected.Y, actual.Y, TOLERANCE);
            Assert.Equal(expected.Z, actual.Z, TOLERANCE);
            Assert.Equal(expected.W, actual.W, TOLERANCE);
        }

        private static void AssertClose(Vector3 expected, Vector3 actual)
        {
            Assert.Equal(expected.X, actual.X, TOLERANCE);
            Assert.Equal(expected.Y, actual.Y, TOLERANCE);
            Assert.Equal(expected.Z, actual.Z, TOLERANCE);
        }

        // The engine slerp: weights from the angle of the raw dot product, without flipping either quaternion
        private static Quaternion EngineSlerp(float t, Quaternion p, Quaternion q)
        {
            var angle = MathF.Acos(Math.Clamp(Quaternion.Dot(p, q), -1, 1));
            var sin = MathF.Sin(angle);
            var a = MathF.Sin((1 - t) * angle) / sin;
            var b = MathF.Sin(t * angle) / sin;

            return new Quaternion(a * p.X + b * q.X, a * p.Y + b * q.Y, a * p.Z + b * q.Z, a * p.W + b * q.W);
        }

        [Fact]
        public void ToNumerics_MatchesTheEngineRotationMatrix()
        {
            // 45° about Z, stored w first. The engine's matrix turns +X towards +Y.
            var q = FinQuaternionMath.ToNumerics(new FinQuaternion(0.9238795f, 0, 0, 0.3826834f));

            AssertClose(new Vector3(0.70710677f, 0.70710677f, 0), Vector3.Transform(Vector3.UnitX, q));
        }

        [Fact]
        public void Slerp_DoesNotTakeTheShortPath()
        {
            var p = Quaternion.Identity;
            var q = -Quaternion.CreateFromAxisAngle(Vector3.UnitX, 10 * MathF.PI / 180);

            var result = FinQuaternionMath.Slerp(0.5f, p, q);

            AssertClose(EngineSlerp(0.5f, p, q), result);
            Assert.True(result.W < 0.1f, "the long way round passes far from the identity");
        }

        [Fact]
        public void Slerp_NearlyEqual_ReturnsFirst()
        {
            var p = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3f);
            var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.3005f);

            Assert.Equal(p, FinQuaternionMath.Slerp(0.7f, p, q));
        }

        [Fact]
        public void Squad_IsSlerpOfSlerps()
        {
            var p = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.2f);
            var a = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f);
            var b = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.4f);
            var q = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 1.1f);
            const float T = 0.3f;

            var expected = EngineSlerp(2 * T * (1 - T), EngineSlerp(T, p, q), EngineSlerp(T, a, b));

            AssertClose(expected, FinQuaternionMath.Squad(T, p, a, b, q));
        }

        [Fact]
        public void FromEuler_Ordering0_TurnsAboutXThenYThenZ()
        {
            var angles = new Vector3(0.1f, 0.2f, 0.3f);
            var v = new Vector3(1, 2, 3);
            var expected = Vector3.Transform(
                Vector3.Transform(Vector3.Transform(v, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.1f)),
                    Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.2f)),
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.3f));

            AssertClose(expected, Vector3.Transform(v, FinQuaternionMath.FromEuler(angles, 0)));
        }

        [Theory]
        [InlineData(1, 0, 2, 1)]
        [InlineData(2, 1, 2, 0)]
        [InlineData(3, 1, 0, 2)]
        [InlineData(4, 2, 0, 1)]
        [InlineData(5, 2, 1, 0)]
        [InlineData(6, 0, 1, 0)]
        [InlineData(7, 1, 2, 1)]
        public void FromEuler_UsesTheOrderingTable(int ordering, int first, int second, int third)
        {
            var angles = new Vector3(0.1f, 0.2f, 0.3f);
            var axes = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
            var v = new Vector3(1, 2, 3);
            var expected = v;
            foreach (var axis in new[] { first, second, third })
            {
                expected = Vector3.Transform(expected, Quaternion.CreateFromAxisAngle(axes[axis], angles[axis]));
            }

            AssertClose(expected, Vector3.Transform(v, FinQuaternionMath.FromEuler(angles, ordering)));
        }

        [Fact]
        public void FromEuler_UnknownOrdering_Throws()
        {
            Assert.Throws<NotSupportedException>(() => FinQuaternionMath.FromEuler(Vector3.Zero, 8));
        }
    }
}
