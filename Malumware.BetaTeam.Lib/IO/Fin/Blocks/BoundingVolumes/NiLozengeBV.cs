using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes
{
    // A parallelogram (origin and two edges) with a radius around it
    public class NiLozengeBV : NiBoundingVolume
    {
        public Vector3 Origin { get; private set; }
        public Vector3 Edge0 { get; private set; }
        public Vector3 Edge1 { get; private set; }
        public float Radius { get; private set; }

        internal override void Load(FinBlockReader reader, int depth)
        {
            Origin = reader.ReadVector3();
            Edge0 = reader.ReadVector3();
            Edge1 = reader.ReadVector3();
            Radius = reader.ReadSingle();
        }
    }
}
