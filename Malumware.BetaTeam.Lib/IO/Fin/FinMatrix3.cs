namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // Nine floats as stored in the file: three groups of three. Whether a group is a row or a column in the
    // engine's maths is for exporters to decide; the reader keeps file order.
    public readonly record struct FinMatrix3(
        float M11, float M12, float M13,
        float M21, float M22, float M23,
        float M31, float M32, float M33)
    {
        public static FinMatrix3 Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);
    }
}
