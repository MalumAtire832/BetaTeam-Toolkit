namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks.BoundingVolumes
{
    // A collision shape, stored inline in its NiAVObject as a type number followed by the shape
    public abstract class NifBoundingVolume
    {
        protected abstract uint Type { get; }

        internal void Write(NifWriter writer)
        {
            writer.WriteUInt32(Type);
            WriteShape(writer);
        }

        protected abstract void WriteShape(NifWriter writer);
    }
}
