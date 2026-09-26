namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Digital Domain's addition to a texture property: which flip animation drives it
    public class TexturePropExtraData : NiExtraData
    {
        public FinRef<NiFlipTextures> FlipTextures { get; private set; } = new(0);
        public int Index { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            FlipTextures = reader.ReadRef<NiFlipTextures>();
            Index = reader.ReadInt32();
        }
    }
}
