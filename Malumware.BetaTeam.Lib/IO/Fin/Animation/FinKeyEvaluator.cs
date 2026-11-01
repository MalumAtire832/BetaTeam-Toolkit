using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Evaluates key lists the way the engine does (NiAnimation.dll: Ni3dsAnimationNode::GenPosInterp, GenRotInterp,
    // GenEulerInterp, DetermineVisibility and each key type's Interpolate). Times are ticks. At or before the first
    // key the first key's value holds, at or after the last key the last one's; a time equal to a key's gives that
    // key; anything else interpolates from the key before to the key after with t from 0 to 1.
    public static class FinKeyEvaluator
    {
        public static Vector3 Position(FinKeyGroup<FinPosKey> keys, float ticks)
        {
            var list = Present(keys);
            var (index, t, exact) = Locate(list.Select(key => key.Time).ToList(), ticks);
            var key = list[index];
            if (exact)
            {
                return key.Value;
            }

            var next = list[index + 1];

            return key switch
            {
                FinLinearPosKey => Vector3.Lerp(key.Value, next.Value, t),
                FinBezierPosKey bezier => Cubic(bezier.Value, bezier.OutTangent, bezier.A, bezier.B, t),
                FinTcbPosKey tcb => Cubic(tcb.Value, tcb.Dd, tcb.A, tcb.B, t),
                _ => throw new NotSupportedException($"Position key type {key.GetType().Name} can't be evaluated"),
            };
        }

        public static Quaternion Rotation(FinKeyGroup<FinRotKey> keys, float ticks)
        {
            var list = Present(keys);
            if (list[0] is FinEulerRotKey euler)
            {
                // The engine only looks at the first key: its three curves carry the whole rotation
                var angles = new Vector3(Axis(euler.X, ticks), Axis(euler.Y, ticks), Axis(euler.Z, ticks));

                return FinQuaternionMath.FromEuler(angles, euler.Unknown3C);
            }

            var (index, t, exact) = Locate(list.Select(key => key.Time).ToList(), ticks);
            var key = list[index];
            if (exact)
            {
                return FinQuaternionMath.ToNumerics(key.Quaternion);
            }

            // The other interpolation modes (by angle, with extra spins) aren't used by any shipped key
            if (key.Unknown2C != 0)
            {
                throw new NotSupportedException($"Rotation interpolation mode {key.Unknown2C} isn't supported");
            }

            var next = list[index + 1];
            var p = FinQuaternionMath.ToNumerics(key.Quaternion);
            var q = FinQuaternionMath.ToNumerics(next.Quaternion);

            return (key, next) switch
            {
                (FinLinearRotKey, _) => FinQuaternionMath.Slerp(t, p, q),
                (FinBezierRotKey a, FinBezierRotKey b) => FinQuaternionMath.Squad(t, p,
                    FinQuaternionMath.ToNumerics(a.Unknown30), FinQuaternionMath.ToNumerics(b.Unknown30), q),
                (FinTcbRotKey a, FinTcbRotKey b) => FinQuaternionMath.Squad(t, p,
                    FinQuaternionMath.ToNumerics(a.QuaternionA), FinQuaternionMath.ToNumerics(b.QuaternionB), q),
                _ => throw new NotSupportedException($"Rotation key type {key.GetType().Name} can't be evaluated"),
            };
        }

        public static float Float(FinKeyGroup<FinFloatKey> keys, float ticks)
        {
            var list = Present(keys);
            var (index, t, exact) = Locate(list.Select(key => key.Time).ToList(), ticks);
            var key = list[index];
            if (exact)
            {
                return key.Value;
            }

            var next = list[index + 1];

            return (key, next) switch
            {
                (FinLinearFloatKey, _) => key.Value + (next.Value - key.Value) * t,
                (FinBezierFloatKey a, FinBezierFloatKey b) => Hermite(t, a.Value, a.OutTangent, b.Value, b.InTangent),
                (FinBaryMorphKey or FinCubicMorphKey, _) =>
                    throw new NotSupportedException("Morph keys are evaluated as weights, not values"),
                (FinTcbFloatKey a, FinTcbFloatKey b) => Hermite(t, a.Value, a.Dd, b.Value, b.Ds),
                _ => throw new NotSupportedException($"Float key type {key.GetType().Name} can't be evaluated"),
            };
        }

        // Ni3dsAnimationNode::DetermineVisibility: a key holds until the next one
        public static bool Visible(IReadOnlyList<FinVisKey> keys, float ticks)
        {
            if (keys.Count == 0)
            {
                throw new ArgumentException("No visibility keys to evaluate", nameof(keys));
            }

            var result = keys[0].Visible;
            foreach (var key in keys)
            {
                if (key.Time > ticks)
                {
                    break;
                }
                result = key.Visible;
            }

            return result;
        }

        // NiBaryMorphKey::MorphInterpolate: each target's weight is its own Hermite curve
        public static float[] MorphWeights(FinKeyGroup<FinFloatKey> keys, float ticks)
        {
            var list = Present(keys)
                .Select(key => key as FinBaryMorphKey
                    ?? throw new NotSupportedException($"Morph key type {key.GetType().Name} can't be evaluated"))
                .ToList();
            var (index, t, exact) = Locate(list.Select(key => key.Time).ToList(), ticks);
            var key = list[index];
            if (exact)
            {
                return key.Weights;
            }

            var next = list[index + 1];
            var weights = key.Weights;
            var outTangents = key.OutTangents;
            var nextWeights = next.Weights;
            var nextInTangents = next.InTangents;
            var count = new[] { weights.Length, outTangents.Length, nextWeights.Length, nextInTangents.Length }.Min();

            return Enumerable.Range(0, count)
                .Select(i => Hermite(t, weights[i], outTangents[i], nextWeights[i], nextInTangents[i]))
                .ToArray();
        }

        public static IReadOnlyList<float> KeyTimes(FinKeyGroup<FinRotKey> keys)
        {
            var list = keys.Keys.OfType<FinRotKey>().ToList();
            if (list.Count > 0 && list[0] is FinEulerRotKey euler)
            {
                return KeyTimes(euler.X).Concat(KeyTimes(euler.Y)).Concat(KeyTimes(euler.Z)).ToList();
            }

            return list.Select(key => key.Time).ToList();
        }

        public static IReadOnlyList<float> KeyTimes(FinKeyGroup<FinPosKey> keys)
        {
            return keys.Keys.OfType<FinPosKey>().Select(key => key.Time).ToList();
        }

        public static IReadOnlyList<float> KeyTimes(FinKeyGroup<FinFloatKey> keys)
        {
            return keys.Keys.OfType<FinFloatKey>().Select(key => key.Time).ToList();
        }

        // One Euler axis; an axis without keys stays at 0
        private static float Axis(FinKeyGroup<FinFloatKey> keys, float ticks)
        {
            return keys.Keys.Any(key => key is not null) ? Float(keys, ticks) : 0;
        }

        private static List<TKey> Present<TKey>(FinKeyGroup<TKey> keys)
            where TKey : class
        {
            var list = keys.Keys.OfType<TKey>().ToList();
            if (list.Count == 0)
            {
                throw new ArgumentException("No keys to evaluate", nameof(keys));
            }

            return list;
        }

        // The key a time falls on or after, how far it is towards the next key, and whether it is exactly on a key
        private static (int Index, float T, bool Exact) Locate(IReadOnlyList<float> times, float ticks)
        {
            if (ticks <= times[0])
            {
                return (0, 0, true);
            }
            if (ticks >= times[^1])
            {
                return (times.Count - 1, 0, true);
            }

            for (var i = 0; i < times.Count - 1; i++)
            {
                if (times[i] == ticks)
                {
                    return (i, 0, true);
                }
                if (times[i] < ticks && ticks < times[i + 1])
                {
                    return (i, (ticks - times[i]) / (times[i + 1] - times[i]), false);
                }
            }

            // Keys out of order: the engine would find no segment either and keep the last key it looked at
            return (times.Count - 1, 0, true);
        }

        // Position Bezier and TCB keys store the segment's cubic: P + t(c1 + t(c2 + t·c3))
        private static Vector3 Cubic(Vector3 value, Vector3 c1, Vector3 c2, Vector3 c3, float t)
        {
            return value + t * (c1 + t * (c2 + t * c3));
        }

        // NiInterpScalar::Bezier, shared by float Bezier, float TCB and morph weights
        private static float Hermite(float t, float v0, float outTangent, float v1, float inTangent)
        {
            var delta = v1 - v0;

            return ((3 * delta - (2 * outTangent + inTangent) + (outTangent + inTangent - 2 * delta) * t) * t + outTangent) * t + v0;
        }
    }
}
