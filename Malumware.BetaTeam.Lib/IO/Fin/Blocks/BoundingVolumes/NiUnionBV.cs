namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes
{
    // Everything inside any of the volumes
    public class NiUnionBV : NiBoundingVolume
    {
        public IReadOnlyList<NiBoundingVolume> Volumes { get; private set; } = [];

        internal override void Load(FinBlockReader reader, int depth)
        {
            Volumes = ReadVolumes(reader, depth);
        }
    }
}
