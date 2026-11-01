using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    // The game's colours are display values: its renderer blends and multiplies them as they are, the way 2000-era
    // hardware did. glTF colours are linear, so they're converted with the sRGB transfer function. A renderer that
    // works in linear space then multiplies them back into the same products the game showed.
    internal static class FinGltfColor
    {
        public static float ToLinear(float value)
        {
            return value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        // Alpha is a coverage amount, not a colour, so it stays as it is
        public static Vector4 ToLinear(float r, float g, float b, float a)
        {
            return new Vector4(ToLinear(r), ToLinear(g), ToLinear(b), a);
        }
    }
}
