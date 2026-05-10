namespace Malumware.BetaTeam.Lib.IO.Tga
{
    /// <summary>Ten-byte image specification embedded in the TGA header</summary>
    public record TgaImageSpec
    {
        /// <summary>X coordinate of the lower-left corner of the image</summary>
        public ushort XOrigin { get; }

        /// <summary>Y coordinate of the lower-left corner of the image</summary>
        public ushort YOrigin { get; }

        /// <summary>Width of the image in pixels</summary>
        public ushort Width { get; }

        /// <summary>Height of the image in pixels</summary>
        public ushort Height { get; }

        /// <summary>Number of bits per pixel (8, 15, 16, 24, or 32)</summary>
        public byte PixelDepth { get; }

        /// <summary>Alpha channel depth (bits 3–0), pixel ordering (bits 5–4), and data interleaving mode (bits 7–6)</summary>
        public byte ImageDescriptor { get; }

        public TgaImageSpec(ushort xOrigin, ushort yOrigin, ushort width, ushort height, byte pixelDepth, byte imageDescriptor)
        {
            XOrigin = xOrigin;
            YOrigin = yOrigin;
            Width = width;
            Height = height;
            PixelDepth = pixelDepth;
            ImageDescriptor = imageDescriptor;
        }
    }
}