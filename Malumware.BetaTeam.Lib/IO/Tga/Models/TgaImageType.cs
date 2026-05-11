namespace Malumware.BetaTeam.Lib.IO.Tga.Models
{
    /// <summary>Compression and color encoding of the image data</summary>
    public enum TgaImageType : byte
    {
        /// <summary>No image data is present</summary>
        NoImageData = 0,

        /// <summary>Uncompressed color-mapped image</summary>
        UncompressedColorMapped = 1,

        /// <summary>Uncompressed true-color image</summary>
        UncompressedTrueColor = 2,

        /// <summary>Uncompressed black-and-white (grayscale) image</summary>
        UncompressedGrayscale = 3,

        /// <summary>Run-length encoded color-mapped image</summary>
        RleColorMapped = 9,

        /// <summary>Run-length encoded true-color image</summary>
        RleTrueColor = 10,

        /// <summary>Run-length encoded black-and-white (grayscale) image</summary>
        RleGrayscale = 11,
    }
}