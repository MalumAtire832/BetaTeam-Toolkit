namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Picks the child whose range contains the camera's distance; range i belongs to child i
    public class NiLODNode : NiSwitchNode
    {
        public FinLodRange[] Ranges { get; private set; } = [];
        // Engine: GetPositionInRange; its effect isn't confirmed
        public bool PositionInRange { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            // Near, far and center; the engine keeps world-space copies of these that aren't saved
            var count = reader.ReadCount(5 * sizeof(float));
            Ranges = reader.ReadArray(count, r => new FinLodRange(r.ReadSingle(), r.ReadSingle(), r.ReadVector3()));
            PositionInRange = reader.ReadByte() != 0;
        }
    }
}
