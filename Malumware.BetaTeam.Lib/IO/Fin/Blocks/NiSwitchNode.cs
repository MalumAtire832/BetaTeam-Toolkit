namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Shows one of its children at a time
    public abstract class NiSwitchNode : NiNode
    {
        // Engine: GetIndex
        public int ActiveChildIndex { get; private set; }

        // Engine: UpdateOnlyActiveChild
        public bool UpdateOnlyActiveChild { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            ActiveChildIndex = reader.ReadInt32();
            UpdateOnlyActiveChild = reader.ReadByte() != 0;
        }
    }
}
