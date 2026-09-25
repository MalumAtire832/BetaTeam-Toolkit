namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Animates a NiTextureProperty by stepping through its images. An action in engine terms: the engine runs it
    // once loaded, so no other block refers to it.
    public class NiFlipTextures : NiObject
    {
        public uint OutOfBound { get; private set; }
        public float Rate { get; private set; }
        public float StartTime { get; private set; }

        // Seconds per frame = CycleTime * Rate / number of images
        public float CycleTime { get; private set; }

        public FinRef<NiTextureProperty> Textures { get; private set; } = new(0);

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            OutOfBound = reader.ReadUInt32();
            Rate = reader.ReadSingle();
            StartTime = reader.ReadSingle();
            CycleTime = reader.ReadSingle();
            Textures = reader.ReadRef<NiTextureProperty>();
        }
    }
}
