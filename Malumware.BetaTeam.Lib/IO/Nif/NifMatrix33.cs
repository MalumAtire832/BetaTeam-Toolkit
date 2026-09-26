namespace Malumware.BetaTeam.Lib.IO.Nif
{
    // Nine floats in the order they are written to the file
    public readonly record struct NifMatrix33(
        float M11, float M12, float M13,
        float M21, float M22, float M23,
        float M31, float M32, float M33)
    {
        public static NifMatrix33 Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);
    }
}
