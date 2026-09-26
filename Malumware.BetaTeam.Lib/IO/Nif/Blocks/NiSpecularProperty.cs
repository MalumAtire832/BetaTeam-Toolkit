namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiSpecularProperty : NiProperty
    {
        public bool Enabled { get; set; }

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteUInt16(Enabled ? (ushort)1 : (ushort)0);
        }
    }
}
