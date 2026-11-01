using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Quaternion operations as NetImmerse implements them (NiMain.dll NiQuaternion), so sampled rotations match the
    // game. System.Numerics uses the same Hamilton product and the same rotation convention.
    internal static class FinQuaternionMath
    {
        // Below this sine of the angle between two quaternions the engine's slerp returns the first one
        private const float MIN_SLERP_SINE = 0.001f;

        // NiEulerRotKey::ms_Orderings: the axis applied first, second and third for each ordering
        private static readonly int[][] ORDERINGS =
        [
            [0, 1, 2], [0, 2, 1], [1, 2, 0], [1, 0, 2], [2, 0, 1], [2, 1, 0], [0, 1, 0], [1, 2, 1],
        ];

        private static readonly Vector3[] AXES = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];

        // The engine stores w first; its ToRotation gives the usual rotation matrix for (w, x, y, z)
        public static Quaternion ToNumerics(FinQuaternion q)
        {
            return new Quaternion(q.X, q.Y, q.Z, q.W);
        }

        // NiQuaternion::Slerp: the angle comes from the raw dot product, so unlike System.Numerics it never flips a
        // quaternion to take the shorter way round
        public static Quaternion Slerp(float t, Quaternion p, Quaternion q)
        {
            var dot = Quaternion.Dot(p, q);
            var angle = dot <= -1 ? MathF.PI : dot >= 1 ? 0 : MathF.Acos(dot);
            var sine = MathF.Sin(angle);
            if (sine < MIN_SLERP_SINE)
            {
                return p;
            }

            var a = MathF.Sin((1 - t) * angle) / sine;
            var b = MathF.Sin(t * angle) / sine;

            return new Quaternion(a * p.X + b * q.X, a * p.Y + b * q.Y, a * p.Z + b * q.Z, a * p.W + b * q.W);
        }

        // NiQuaternion::Squad, with a and b the control quaternions of the segment's start and end
        public static Quaternion Squad(float t, Quaternion p, Quaternion a, Quaternion b, Quaternion q)
        {
            return Slerp(2 * t * (1 - t), Slerp(t, p, q), Slerp(t, a, b));
        }

        // Ni3dsAnimationNode::GenEulerInterp: one rotation per axis, combined so the ordering's first axis is
        // applied first
        public static Quaternion FromEuler(Vector3 angles, int ordering)
        {
            if (ordering < 0 || ordering >= ORDERINGS.Length)
            {
                throw new NotSupportedException($"Euler ordering {ordering} isn't one the engine has");
            }

            var result = Quaternion.Identity;
            foreach (var axis in ORDERINGS[ordering])
            {
                result = Quaternion.CreateFromAxisAngle(AXES[axis], angles[axis]) * result;
            }

            return result;
        }
    }
}
