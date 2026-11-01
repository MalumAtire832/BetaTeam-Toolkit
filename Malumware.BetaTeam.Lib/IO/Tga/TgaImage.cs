namespace Malumware.BetaTeam.Lib.IO.Tga
{
    // Decoded pixels: RGBA, 8 bits each, rows top to bottom. Alpha is 255 throughout when HasAlpha is false.
    public sealed class TgaImage
    {
        public int Width { get; }
        public int Height { get; }
        public bool HasAlpha { get; }
        public byte[] Pixels { get; }

        public TgaImage(int width, int height, bool hasAlpha, byte[] pixels)
        {
            Width = width;
            Height = height;
            HasAlpha = hasAlpha;
            Pixels = pixels;
        }
    }
}
