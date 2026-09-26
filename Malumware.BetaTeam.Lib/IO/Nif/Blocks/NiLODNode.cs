using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    // Range i belongs to child i
    public class NiLODNode : NiSwitchNode
    {
        public Vector3 LodCenter { get; set; }
        public List<NifLodRange> LodLevels { get; } = [];

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteVector3(LodCenter);
            writer.WriteCount(LodLevels.Count);
            foreach (var level in LodLevels)
            {
                writer.WriteSingle(level.Near);
                writer.WriteSingle(level.Far);
            }
        }
    }
}
