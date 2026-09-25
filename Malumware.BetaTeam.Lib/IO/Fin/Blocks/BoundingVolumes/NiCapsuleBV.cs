using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes
{
    // A line segment (origin plus direction) with a radius around it
    public class NiCapsuleBV : NiBoundingVolume
    {
        public Vector3 Origin { get; private set; }
        public Vector3 Direction { get; private set; }
        public float Radius { get; private set; }
        public bool Inverted { get; private set; }

        internal override void Load(FinBlockReader reader, int depth)
        {
            Origin = reader.ReadVector3();
            Direction = reader.ReadVector3();
            Radius = reader.ReadSingle();
            Inverted = reader.ReadByte() != 0;
        }
    }
}
