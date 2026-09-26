namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // A list of images, of which Index is the one shown (NiFlipTextures animates through them)
    public class NiTextureProperty : NiProperty
    {
        public int Index { get; private set; }
        public IReadOnlyList<FinRef<NiImage>> Images { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Index = reader.ReadInt32();
            Images = reader.ReadRefList<NiImage>();
        }
    }
}
