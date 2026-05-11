namespace Malumware.BetaTeam.Lib.IO.Tga.Models
{
    public static class TgaImageTypeExtensions
    {
        public static bool IsRunLengthEncoded(this TgaImageType imageType)
        {
            return imageType 
                is TgaImageType.RleColorMapped 
                or TgaImageType.RleTrueColor 
                or TgaImageType.RleGrayscale;
        }

        public static bool IsUncompressed(this TgaImageType imageType)
        {
            return imageType 
                is TgaImageType.UncompressedColorMapped 
                or TgaImageType.UncompressedTrueColor 
                or TgaImageType.UncompressedGrayscale;            
        }

        public static bool IsColorMapped(this TgaImageType imageType)
        {
            return imageType
                is TgaImageType.UncompressedColorMapped
                or TgaImageType.RleColorMapped;
        }
        
        public static bool IsTrueColor(this TgaImageType imageType)
        {
            return imageType
                is TgaImageType.UncompressedTrueColor
                or TgaImageType.RleTrueColor;
        }
        
        public static bool IsGrayscale(this TgaImageType imageType)
        {
            return imageType
                is TgaImageType.UncompressedGrayscale
                or TgaImageType.RleGrayscale;
        }
    }
}