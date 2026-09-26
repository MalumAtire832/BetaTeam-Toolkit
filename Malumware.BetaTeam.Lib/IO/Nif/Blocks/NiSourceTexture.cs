namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiSourceTexture : NiTexture
    {
        // "Use the default" for each of the format preferences
        public const uint PIXEL_LAYOUT_DEFAULT = 6;
        public const uint MIPMAP_FORMAT_DEFAULT = 2;
        public const uint ALPHA_FORMAT_DEFAULT = 3;

        // Null for a texture without a file. Embedded pixel data isn't supported, so such a texture has no image.
        public string? FileName { get; set; }
        public uint PixelLayout { get; set; } = PIXEL_LAYOUT_DEFAULT;
        public uint MipmapFormat { get; set; } = MIPMAP_FORMAT_DEFAULT;
        public uint AlphaFormat { get; set; } = ALPHA_FORMAT_DEFAULT;
        public bool IsStatic { get; set; } = true;

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            if (FileName is not null)
            {
                writer.WriteByte(1);
                writer.WriteSizedString(FileName);
            }
            else
            {
                // Neither external nor internal, so no pixel data link follows
                writer.WriteByte(0);
                writer.WriteByte(0);
            }
            writer.WriteUInt32(PixelLayout);
            writer.WriteUInt32(MipmapFormat);
            writer.WriteUInt32(AlphaFormat);
            writer.WriteByte(IsStatic ? (byte)1 : (byte)0);
        }
    }
}
