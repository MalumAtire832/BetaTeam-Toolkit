using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks.BoundingVolumes
{
    // An oriented box: a center, three axes and the half-size along each axis
    public class NifBoxBV : NifBoundingVolume
    {
        public Vector3 Center { get; set; }
        public Vector3[] Axes { get; set; } = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];
        public Vector3 Extents { get; set; }

        protected override uint Type => 1;

        protected override void WriteShape(NifWriter writer)
        {
            if (Axes.Length != 3)
            {
                throw new InvalidOperationException($"A box has 3 axes, not {Axes.Length}");
            }

            writer.WriteVector3(Center);
            foreach (var axis in Axes)
            {
                writer.WriteVector3(axis);
            }
            writer.WriteVector3(Extents);
        }
    }
}
