using Malumware.BetaTeam.Lib.IO.Tga.Models;

namespace Malumware.BetaTeam.Lib.IO.Tga.Conversion
{
    public class TgaImageConverter
    {
        /// <summary>
        ///     Converts a TGA image to a PNG-encoded byte array.
        /// </summary>
        public byte[] ToPng(TgaImage image)
        {
            return image.Header.ImageType.IsColorMapped() 
                ? new ColorMappedTgaConverter().ToPng(image) 
                : new DirectTgaConverter().ToPng(image);
        }

        /// <summary>
        ///     Converts a TGA image and writes the result as a PNG file.
        /// </summary>
        public void Convert(TgaImage image, string outputPath)
        {
            File.WriteAllBytes(outputPath, ToPng(image));
        }
    }
}
