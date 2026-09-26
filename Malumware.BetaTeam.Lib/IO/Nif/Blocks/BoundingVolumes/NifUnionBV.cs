namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks.BoundingVolumes
{
    // Everything inside any of the volumes
    public class NifUnionBV : NifBoundingVolume
    {
        public List<NifBoundingVolume> Volumes { get; } = [];

        protected override uint Type => 4;

        protected override void WriteShape(NifWriter writer)
        {
            writer.WriteCount(Volumes.Count);
            foreach (var volume in Volumes)
            {
                volume.Write(writer);
            }
        }
    }
}
