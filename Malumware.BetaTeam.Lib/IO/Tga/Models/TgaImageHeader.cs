namespace Malumware.BetaTeam.Lib.IO.Tga.Models
{
    /// <summary>Parsed 18-byte fixed header at the start of every TGA file</summary>
    public record TgaImageHeader
    {
        /// <summary>Total size in bytes of the fixed header at the start of a TGA file</summary>
        public const int SIZE = 18;

        /// <summary>Length of the image ID field in bytes (0–255); zero means no image ID is present</summary>
        public byte IdLength { get; }

        /// <summary>Indicates whether a color map is present: 0 = none, 1 = present</summary>
        public byte ColorMapType { get; }

        /// <summary>Compression and color encoding of the image data</summary>
        public TgaImageType ImageType { get; }

        /// <summary>Color map origin, length, and bit depth</summary>
        public TgaColorMapSpec ColorMapSpec { get; }

        /// <summary>Image dimensions, pixel depth, and descriptor flags</summary>
        public TgaImageSpec ImageSpec { get; }

        public TgaImageHeader(byte idLength, byte colorMapType, TgaImageType imageType, TgaColorMapSpec colorMapSpec, TgaImageSpec imageSpec)
        {
            IdLength = idLength;
            ColorMapType = colorMapType;
            ImageType = imageType;
            ColorMapSpec = colorMapSpec;
            ImageSpec = imageSpec;
        }
    }
}