namespace Malumware.BetaTeam.Lib.IO.Tga
{
    // The fixed header of a Truevision TGA file
    public record TgaHeader
    {
        public const int SIZE = 18;

        // Descriptor bit 5; without it the rows are stored bottom to top
        private const byte TOP_TO_BOTTOM_BIT = 0x20;

        public byte IdLength { get; }
        public byte ColorMapType { get; }
        public byte ImageType { get; }
        public ushort ColorMapLength { get; }
        public byte ColorMapEntrySize { get; }
        public ushort Width { get; }
        public ushort Height { get; }
        public byte PixelDepth { get; }
        public byte Descriptor { get; }

        public bool TopToBottom => (Descriptor & TOP_TO_BOTTOM_BIT) != 0;
        public bool IsRunLength => ImageType is 10 or 11;
        public bool IsGreyscale => ImageType is 3 or 11;

        // The subset the game uses: true colour (2) and greyscale (3), raw or run-length encoded (10, 11). The engine
        // also reads colour-mapped images (1, 9), but no shipped file is one.
        public bool IsValid => ColorMapType <= 1
            && (IsGreyscale ? PixelDepth == 8 : (ImageType is 2 or 10) && (PixelDepth is 24 or 32));

        public TgaHeader(
            byte idLength,
            byte colorMapType,
            byte imageType,
            ushort colorMapLength,
            byte colorMapEntrySize,
            ushort width,
            ushort height,
            byte pixelDepth,
            byte descriptor)
        {
            IdLength = idLength;
            ColorMapType = colorMapType;
            ImageType = imageType;
            ColorMapLength = colorMapLength;
            ColorMapEntrySize = colorMapEntrySize;
            Width = width;
            Height = height;
            PixelDepth = pixelDepth;
            Descriptor = descriptor;
        }
    }
}
