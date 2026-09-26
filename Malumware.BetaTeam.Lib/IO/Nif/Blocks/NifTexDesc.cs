namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    // One texture slot of a NiTexturingProperty
    public sealed class NifTexDesc
    {
        public const uint CLAMP_WRAP_S_WRAP_T = 3;
        public const uint MAX_CLAMP_MODE = 3;
        public const uint FILTER_TRILERP = 2;
        public const uint MAX_FILTER_MODE = 6;

        // PlayStation 2 mipmap settings, at NIF's defaults
        private const short PS2_L = 0;
        private const short PS2_K = -75;

        public NiSourceTexture? Source { get; set; }
        public uint ClampMode { get; set; } = CLAMP_WRAP_S_WRAP_T;
        public uint FilterMode { get; set; } = FILTER_TRILERP;
        public uint UvSet { get; set; }

        internal void Write(NifWriter writer)
        {
            writer.WriteRef(Source);
            writer.WriteUInt32(ClampMode);
            writer.WriteUInt32(FilterMode);
            writer.WriteUInt32(UvSet);
            writer.WriteInt16(PS2_L);
            writer.WriteInt16(PS2_K);
            // Unknown short without a documented meaning
            writer.WriteUInt16(0);
        }
    }
}
