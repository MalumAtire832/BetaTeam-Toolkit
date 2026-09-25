using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes
{
    public class NiSphereBV : NiBoundingVolume
    {
        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }

        // Collides from the inside instead of the outside
        public bool Inverted { get; private set; }

        internal override void Load(FinBlockReader reader, int depth)
        {
            Center = reader.ReadVector3();
            Radius = reader.ReadSingle();
            Inverted = reader.ReadByte() != 0;
        }
    }
}
