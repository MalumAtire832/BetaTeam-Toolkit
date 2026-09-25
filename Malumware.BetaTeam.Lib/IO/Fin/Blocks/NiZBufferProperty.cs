namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiZBufferProperty : NiProperty
    {
        public bool ZBufferTest { get; private set; }
        public bool ZBufferWrite { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            ZBufferTest = reader.ReadByte() != 0;
            ZBufferWrite = reader.ReadByte() != 0;
        }
    }
}
