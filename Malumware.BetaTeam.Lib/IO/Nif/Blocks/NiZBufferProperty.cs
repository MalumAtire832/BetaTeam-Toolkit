namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiZBufferProperty : NiProperty
    {
        public const ushort FLAG_TEST = 0x0001;
        public const ushort FLAG_WRITE = 0x0002;

        public ushort Flags { get; set; } = FLAG_TEST | FLAG_WRITE;

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteUInt16(Flags);
        }
    }
}
