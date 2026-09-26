namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiShadeProperty : NiProperty
    {
        public bool Smooth { get; set; } = true;

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteUInt16(Smooth ? (ushort)1 : (ushort)0);
        }
    }
}
