namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Animates a NiTextureProperty by stepping through its images. An action in engine terms: the engine runs it
    // once loaded, so no other block refers to it.
    public class NiFlipTextures : NiObject
    {
        // Engine: GetOutOfBound
        public uint OutOfBound { get; private set; }

        // Engine: SetRate
        public float Rate { get; private set; }

        // Engine: GetStartTime
        public float StartTime { get; private set; }

        // Seconds per frame = CycleTime * Rate / number of images (engine: GetSecsPerFrame). The name is ours.
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
