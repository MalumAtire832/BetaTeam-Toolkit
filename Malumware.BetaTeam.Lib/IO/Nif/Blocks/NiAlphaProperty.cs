namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiAlphaProperty : NiProperty
    {
        public const uint FLAG_BLEND = 0x0001;
        public const int SOURCE_BLEND_SHIFT = 1;
        public const int DESTINATION_BLEND_SHIFT = 5;
        public const uint MAX_BLEND_FUNCTION = 15;
        public const uint BLEND_SOURCE_ALPHA = 6;
        public const uint BLEND_INVERSE_SOURCE_ALPHA = 7;

        // Blend enable, source and destination blend functions, alpha test enable and function, no sorter
        public ushort Flags { get; set; }
        public byte Threshold { get; set; }

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteUInt16(Flags);
            writer.WriteByte(Threshold);
        }
    }
}
