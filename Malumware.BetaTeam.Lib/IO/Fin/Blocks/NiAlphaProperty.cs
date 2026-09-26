namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiAlphaProperty : NiProperty
    {
        public bool AlphaBlending { get; private set; }
        public uint SourceBlendMode { get; private set; }
        public uint DestinationBlendMode { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            AlphaBlending = reader.ReadByte() != 0;
            SourceBlendMode = reader.ReadUInt32();
            DestinationBlendMode = reader.ReadUInt32();
        }
    }
}
