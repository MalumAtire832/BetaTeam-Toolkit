using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Position keys; colour animators use them for red, green and blue as well
    public abstract record FinPosKey(float Time, Vector3 Value);

    public sealed record FinLinearPosKey(float Time, Vector3 Value) : FinPosKey(Time, Value);

    // A and B are values the exporter precomputed (names inferred from later NetImmerse versions)
    public sealed record FinBezierPosKey(float Time, Vector3 Value, Vector3 InTangent, Vector3 OutTangent, Vector3 A, Vector3 B)
        : FinPosKey(Time, Value);

    public sealed record FinTcbPosKey(
        float Time, Vector3 Value, float Tension, float Continuity, float Bias, Vector3 Ds, Vector3 Dd, Vector3 A, Vector3 B)
        : FinPosKey(Time, Value);
}
