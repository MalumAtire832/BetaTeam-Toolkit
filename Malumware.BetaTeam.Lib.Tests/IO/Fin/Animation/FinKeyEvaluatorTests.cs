using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Animation
{
    public class FinKeyEvaluatorTests
    {
        private const float TOLERANCE = 1e-5f;
        private static readonly FinQuaternion IDENTITY = new(1, 0, 0, 0);

        private static FinKeyGroup<TKey> Group<TKey>(uint type, params TKey[] keys)
            where TKey : class
        {
            return new FinKeyGroup<TKey>(type, keys);
        }

        private static FinQuaternion Fin(Quaternion q)
        {
            return new FinQuaternion(q.W, q.X, q.Y, q.Z);
        }

        private static FinQuaternion AboutX(float angle)
        {
            return Fin(Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle));
        }

        private static float Hermite(float t, float v0, float outTangent, float v1, float inTangent)
        {
            var delta = v1 - v0;

            return ((3 * delta - (2 * outTangent + inTangent) + (outTangent + inTangent - 2 * delta) * t) * t + outTangent) * t + v0;
        }

        private static void AssertClose(Vector3 expected, Vector3 actual)
        {
            Assert.Equal(expected.X, actual.X, TOLERANCE);
            Assert.Equal(expected.Y, actual.Y, TOLERANCE);
            Assert.Equal(expected.Z, actual.Z, TOLERANCE);
        }

        private static void AssertClose(Quaternion expected, Quaternion actual)
        {
            Assert.Equal(expected.X, actual.X, TOLERANCE);
            Assert.Equal(expected.Y, actual.Y, TOLERANCE);
            Assert.Equal(expected.Z, actual.Z, TOLERANCE);
            Assert.Equal(expected.W, actual.W, TOLERANCE);
        }

        private static uint[] Bits(params float[] values)
        {
            return values.Select(BitConverter.SingleToUInt32Bits).ToArray();
        }

        [Fact]
        public void Position_Linear_Lerps()
        {
            var keys = Group<FinPosKey>(1,
                new FinLinearPosKey(0, Vector3.Zero),
                new FinLinearPosKey(100, new Vector3(10, 20, 30)));

            AssertClose(new Vector3(2.5f, 5, 7.5f), FinKeyEvaluator.Position(keys, 25));
        }

        [Fact]
        public void Position_Bezier_UsesPrecomputedCubic()
        {
            var keys = Group<FinPosKey>(2,
                new FinBezierPosKey(0, new Vector3(1, 0, 0), new Vector3(9, 9, 9), new Vector3(2, 0, 0),
                    new Vector3(3, 0, 0), new Vector3(4, 0, 0)),
                new FinBezierPosKey(100, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero));

            AssertClose(new Vector3(3.25f, 0, 0), FinKeyEvaluator.Position(keys, 50));
        }

        [Fact]
        public void Position_Tcb_UsesDdAndAB()
        {
            var keys = Group<FinPosKey>(3,
                new FinTcbPosKey(0, new Vector3(0, 1, 0), 0, 0, 0, new Vector3(0, 9, 0), new Vector3(0, 2, 0),
                    new Vector3(0, 3, 0), new Vector3(0, 4, 0)),
                new FinTcbPosKey(100, Vector3.Zero, 0, 0, 0, Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero));

            AssertClose(new Vector3(0, 3.25f, 0), FinKeyEvaluator.Position(keys, 50));
        }

        [Fact]
        public void Position_ClampsOutsideTheKeys()
        {
            var keys = Group<FinPosKey>(1,
                new FinLinearPosKey(10, new Vector3(1, 1, 1)),
                new FinLinearPosKey(20, new Vector3(2, 2, 2)));

            AssertClose(new Vector3(1, 1, 1), FinKeyEvaluator.Position(keys, 0));
            AssertClose(new Vector3(2, 2, 2), FinKeyEvaluator.Position(keys, 30));
        }

        [Fact]
        public void Evaluate_EqualKeyTimes_TakesTheLaterKey()
        {
            var keys = Group<FinPosKey>(1,
                new FinLinearPosKey(0, Vector3.Zero),
                new FinLinearPosKey(50, new Vector3(1, 0, 0)),
                new FinLinearPosKey(50, new Vector3(5, 0, 0)),
                new FinLinearPosKey(100, new Vector3(7, 0, 0)));

            AssertClose(new Vector3(1, 0, 0), FinKeyEvaluator.Position(keys, 50));
            AssertClose(new Vector3(6, 0, 0), FinKeyEvaluator.Position(keys, 75));
        }

        [Fact]
        public void Float_Linear_Lerps()
        {
            var keys = Group<FinFloatKey>(1, new FinLinearFloatKey(0, 2), new FinLinearFloatKey(10, 4));

            Assert.Equal(3, FinKeyEvaluator.Float(keys, 5), TOLERANCE);
        }

        [Fact]
        public void Float_Bezier_IsHermite()
        {
            var keys = Group<FinFloatKey>(2,
                new FinBezierFloatKey(0, 0, 7, 3),
                new FinBezierFloatKey(100, 1, -2, 8));

            Assert.Equal(Hermite(0.25f, 0, 3, 1, -2), FinKeyEvaluator.Float(keys, 25), TOLERANCE);
        }

        [Fact]
        public void Float_Tcb_UsesDdThenNextDs()
        {
            var keys = Group<FinFloatKey>(3,
                new FinTcbFloatKey(0, 1, 0, 0, 0, 9, 0.5f),
                new FinTcbFloatKey(100, 2, 0, 0, 0, -1, 9));

            Assert.Equal(Hermite(0.6f, 1, 0.5f, 2, -1), FinKeyEvaluator.Float(keys, 60), TOLERANCE);
        }

        [Fact]
        public void Rotation_Linear_Slerps()
        {
            var keys = Group<FinRotKey>(1,
                new FinLinearRotKey(0, 0, Vector3.UnitX, IDENTITY, 0, 0),
                new FinLinearRotKey(100, 0, Vector3.UnitX, AboutX(1), 0, 0));

            AssertClose(Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.25f), FinKeyEvaluator.Rotation(keys, 25));
        }

        [Fact]
        public void Rotation_Bezier_SquadsWithItsOwnControlQuaternion()
        {
            var a = AboutX(0.3f);
            var b = Fin(Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.2f));
            var keys = Group<FinRotKey>(2,
                new FinBezierRotKey(0, 0, Vector3.UnitX, IDENTITY, 0, 0, a, 0),
                new FinBezierRotKey(100, 0, Vector3.UnitX, AboutX(1), 0, 0, b, 0));

            var expected = FinQuaternionMath.Squad(0.4f, Quaternion.Identity, FinQuaternionMath.ToNumerics(a),
                FinQuaternionMath.ToNumerics(b), FinQuaternionMath.ToNumerics(AboutX(1)));

            AssertClose(expected, FinKeyEvaluator.Rotation(keys, 40));
        }

        [Fact]
        public void Rotation_Tcb_SquadsWithAThenB()
        {
            var outgoing = AboutX(0.3f);
            var incoming = Fin(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.2f));
            var unused = Fin(Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f));
            var keys = Group<FinRotKey>(3,
                new FinTcbRotKey(0, 0, Vector3.UnitX, IDENTITY, 0, 0, 0, 0, 0, outgoing, unused, 0, 0),
                new FinTcbRotKey(100, 0, Vector3.UnitX, AboutX(1), 0, 0, 0, 0, 0, unused, incoming, 0, 0));

            var expected = FinQuaternionMath.Squad(0.4f, Quaternion.Identity, FinQuaternionMath.ToNumerics(outgoing),
                FinQuaternionMath.ToNumerics(incoming), FinQuaternionMath.ToNumerics(AboutX(1)));

            AssertClose(expected, FinKeyEvaluator.Rotation(keys, 40));
        }

        [Fact]
        public void Rotation_ExactKeyTime_IsThatKey()
        {
            var keys = Group<FinRotKey>(1,
                new FinLinearRotKey(0, 0, Vector3.UnitX, IDENTITY, 0, 0),
                new FinLinearRotKey(100, 0, Vector3.UnitX, AboutX(0.5f), 0, 0),
                new FinLinearRotKey(200, 0, Vector3.UnitX, AboutX(1), 0, 0));

            AssertClose(FinQuaternionMath.ToNumerics(AboutX(0.5f)), FinKeyEvaluator.Rotation(keys, 100));
        }

        [Fact]
        public void Rotation_Euler_EvaluatesEachAxisCurve()
        {
            var x = Group<FinFloatKey>(1, new FinLinearFloatKey(0, 0), new FinLinearFloatKey(100, 1));
            var keys = Group<FinRotKey>(4,
                new FinEulerRotKey(0, 0, Vector3.UnitX, IDENTITY, 0, 0, 0, x, FinKeyGroup<FinFloatKey>.Empty,
                    FinKeyGroup<FinFloatKey>.Empty));

            AssertClose(Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.5f), FinKeyEvaluator.Rotation(keys, 50));
        }

        [Fact]
        public void Rotation_ModeOtherThanZero_Throws()
        {
            var keys = Group<FinRotKey>(1,
                new FinLinearRotKey(0, 0, Vector3.UnitX, IDENTITY, 0, 1),
                new FinLinearRotKey(100, 0, Vector3.UnitX, AboutX(1), 0, 1));

            var error = Assert.Throws<NotSupportedException>(() => FinKeyEvaluator.Rotation(keys, 50));
            Assert.Contains("1", error.Message);
        }

        [Fact]
        public void Visible_IsAStepFunction()
        {
            var keys = new[] { new FinVisKey(10, false), new FinVisKey(100, true), new FinVisKey(200, false) };

            Assert.False(FinKeyEvaluator.Visible(keys, 0));
            Assert.False(FinKeyEvaluator.Visible(keys, 99));
            Assert.True(FinKeyEvaluator.Visible(keys, 100));
            Assert.True(FinKeyEvaluator.Visible(keys, 199));
            Assert.False(FinKeyEvaluator.Visible(keys, 250));
        }

        [Fact]
        public void MorphWeights_AreHermitePerTarget()
        {
            var keys = Group<FinFloatKey>(5,
                new FinBaryMorphKey(0, 0, 0, 0, 0, 0, 0, Bits(0, 1), Bits(9, 9), Bits(0.5f, 0)),
                new FinBaryMorphKey(100, 0, 0, 0, 0, 0, 0, Bits(1, 0), Bits(-1, 2), Bits(9, 9)));

            var weights = FinKeyEvaluator.MorphWeights(keys, 30);

            Assert.Equal(2, weights.Length);
            Assert.Equal(Hermite(0.3f, 0, 0.5f, 1, -1), weights[0], TOLERANCE);
            Assert.Equal(Hermite(0.3f, 1, 0, 0, 2), weights[1], TOLERANCE);
            Assert.Equal([0, 1], FinKeyEvaluator.MorphWeights(keys, -5));
        }

        [Fact]
        public void KeyTimes_ListEveryCurve()
        {
            var x = Group<FinFloatKey>(1, new FinLinearFloatKey(5, 0), new FinLinearFloatKey(15, 1));
            var z = Group<FinFloatKey>(1, new FinLinearFloatKey(7, 0));
            var keys = Group<FinRotKey>(4,
                new FinEulerRotKey(0, 0, Vector3.UnitX, IDENTITY, 0, 0, 0, x, FinKeyGroup<FinFloatKey>.Empty, z));

            Assert.Equal([5, 15, 7], FinKeyEvaluator.KeyTimes(keys));
        }

        [Fact]
        public void EmptyGroup_Throws()
        {
            Assert.Throws<ArgumentException>(() => FinKeyEvaluator.Position(FinKeyGroup<FinPosKey>.Empty, 0));
        }
    }
}
