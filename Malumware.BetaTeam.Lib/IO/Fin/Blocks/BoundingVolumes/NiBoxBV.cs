using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes
{
    // An oriented box: a center, three axes and the half-size along each axis
    public class NiBoxBV : NiBoundingVolume
    {
        public Vector3 Center { get; private set; }
        public Vector3[] Axes { get; private set; } = [];
        public Vector3 Extents { get; private set; }
        public bool Inverted { get; private set; }

        internal override void Load(FinBlockReader reader, int depth)
        {
            Center = reader.ReadVector3();
            Axes = reader.ReadArray(3, r => r.ReadVector3());
            Extents = reader.ReadVector3();
            Inverted = reader.ReadByte() != 0;
        }
    }
}
