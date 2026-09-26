namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiSwitchNode : NiNode
    {
        public uint Index { get; set; }

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteUInt32(Index);
        }
    }
}
