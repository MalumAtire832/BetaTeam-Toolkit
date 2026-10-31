namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    public abstract record FinFloatKey(float Time, float Value);

    public sealed record FinLinearFloatKey(float Time, float Value) : FinFloatKey(Time, Value);

    public sealed record FinBezierFloatKey(float Time, float Value, float InTangent, float OutTangent)
        : FinFloatKey(Time, Value);

    // Ds and Dd are derivatives the exporter precomputed (names inferred from later NetImmerse versions)
    public record FinTcbFloatKey(float Time, float Value, float Tension, float Continuity, float Bias, float Ds, float Dd)
        : FinFloatKey(Time, Value);

    // Morph keys are TCB float keys with extra data: per morph target a weight (Data0), its in tangent (Data1) and
    // its out tangent (Data2), all floats (NiBaryMorphKey::MorphInterpolate)
    public sealed record FinBaryMorphKey(
        float Time, float Value, float Tension, float Continuity, float Bias, float Ds, float Dd,
        uint[] Data0, uint[] Data1, uint[] Data2)
        : FinTcbFloatKey(Time, Value, Tension, Continuity, Bias, Ds, Dd)
    {
        public float[] Weights => ToFloats(Data0);

        public float[] InTangents => ToFloats(Data1);

        public float[] OutTangents => ToFloats(Data2);

        private static float[] ToFloats(uint[] values)
        {
            return values.Select(BitConverter.UInt32BitsToSingle).ToArray();
        }
    }

    public sealed record FinCubicMorphKey(
        float Time, float Value, float Tension, float Continuity, float Bias, float Ds, float Dd,
        float Unknown20, float Unknown24)
        : FinTcbFloatKey(Time, Value, Tension, Continuity, Bias, Ds, Dd);
}
