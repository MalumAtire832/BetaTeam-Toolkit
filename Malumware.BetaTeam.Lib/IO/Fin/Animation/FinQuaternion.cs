namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Four floats in file order. NetImmerse stores w first (inferred from later versions; the loader only reads four
    // floats).
    public readonly record struct FinQuaternion(float W, float X, float Y, float Z);
}
