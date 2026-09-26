using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks.BoundingVolumes
{
    public class NifSphereBV : NifBoundingVolume
    {
        public Vector3 Center { get; set; }
        public float Radius { get; set; }

        protected override uint Type => 0;

        protected override void WriteShape(NifWriter writer)
        {
            writer.WriteVector3(Center);
            writer.WriteSingle(Radius);
        }
    }
}
