namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    public abstract record FinFloatKey(float Time, float Value);

    public sealed record FinLinearFloatKey(float Time, float Value) : FinFloatKey(Time, Value);

    public sealed record FinBezierFloatKey(float Time, float Value, float InTangent, float OutTangent)
        : FinFloatKey(Time, Value);

    // Ds and Dd are derivatives the exporter precomputed (names inferred from later NetImmerse versions)
    public record FinTcbFloatKey(float Time, float Value, float Tension, float Continuity, float Bias, float Ds, float Dd)
        : FinFloatKey(Time, Value);

    // Morph keys are TCB float keys with extra data; the three arrays hold 4-byte values of unknown type
    public sealed record FinBaryMorphKey(
        float Time, float Value, float Tension, float Continuity, float Bias, float Ds, float Dd,
        uint[] Data0, uint[] Data1, uint[] Data2)
        : FinTcbFloatKey(Time, Value, Tension, Continuity, Bias, Ds, Dd);

    public sealed record FinCubicMorphKey(
        float Time, float Value, float Tension, float Continuity, float Bias, float Ds, float Dd,
        float Unknown20, float Unknown24)
        : FinTcbFloatKey(Time, Value, Tension, Continuity, Bias, Ds, Dd);
}
