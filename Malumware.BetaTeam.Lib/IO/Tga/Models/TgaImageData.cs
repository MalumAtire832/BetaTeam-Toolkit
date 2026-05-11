namespace Malumware.BetaTeam.Lib.IO.Tga.Models
{
    /// <summary>Raw data sections of a TGA file following the fixed header</summary>
    public record TgaImageData
    {
        /// <summary>Optional identifying data; empty when <see cref="TgaImageHeader.IdLength"/> is zero</summary>
        public byte[] ImageId { get; }

        /// <summary>Colour map entries; non-null only when <see cref="TgaImageHeader.ColorMapType"/> is 1</summary>
        public byte[]? ColorMapData { get; }

        /// <summary>Raw pixel data for the image</summary>
        public byte[] PixelData { get; }

        public TgaImageData(byte[] imageId, byte[]? colorMapData, byte[] pixelData)
        {
            ImageId = imageId;
            ColorMapData = colorMapData;
            PixelData = pixelData;
        }
    }
}
