namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes
{
    // Digital Domain's addition: everything inside all of the volumes
    public class DDIntersectionBV : NiBoundingVolume
    {
        public IReadOnlyList<NiBoundingVolume> Volumes { get; private set; } = [];

        internal override void Load(FinBlockReader reader, int depth)
        {
            Volumes = ReadVolumes(reader, depth);
        }
    }
}
