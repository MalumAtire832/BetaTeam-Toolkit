namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes
{
    // Digital Domain's addition: everything on one side of a plane
    public class DDHalfSpaceBV : NiBoundingVolume
    {
        public FinPlane Plane { get; private set; }

        internal override void Load(FinBlockReader reader, int depth)
        {
            Plane = new FinPlane(reader.ReadVector3(), reader.ReadSingle());
        }
    }
}
