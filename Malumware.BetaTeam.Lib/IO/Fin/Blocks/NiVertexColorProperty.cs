namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiVertexColorProperty : NiProperty
    {
        public uint ColorMode { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            ColorMode = reader.ReadUInt32();
        }
    }
}
