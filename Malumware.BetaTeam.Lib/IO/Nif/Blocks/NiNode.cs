namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiNode : NiAVObject
    {
        // Null entries are written as empty links, which keeps the positions of the other children
        public List<NiAVObject?> Children { get; } = [];

        internal override IEnumerable<NiObject?> GetLinks()
        {
            return base.GetLinks().Concat(Children);
        }

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteRefList(Children);
            // Effects: lights aren't exported
            writer.WriteCount(0);
        }
    }
}
