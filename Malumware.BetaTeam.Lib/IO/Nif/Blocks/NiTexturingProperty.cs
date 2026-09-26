namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiTexturingProperty : NiProperty
    {
        public const uint APPLY_MODULATE = 2;
        public const uint MAX_APPLY_MODE = 4;

        // Base to decal 0 (nif.xml's default); the file's texture count says how many slots follow
        public const int TEXTURE_COUNT = 7;

        public ushort Flags { get; set; }
        public uint ApplyMode { get; set; } = APPLY_MODULATE;

        // Indexed by NifTextureSlot; null means the slot is empty
        public NifTexDesc?[] Textures { get; } = new NifTexDesc?[TEXTURE_COUNT];

        internal override IEnumerable<NiObject?> GetLinks()
        {
            return base.GetLinks().Concat(Textures.Select(texture => texture?.Source));
        }

        internal override void Write(NifWriter writer)
        {
            if (Textures[(int)NifTextureSlot.BumpMap] is not null)
            {
                throw new InvalidOperationException("Bump maps need settings this writer doesn't support");
            }

            base.Write(writer);
            writer.WriteUInt16(Flags);
            writer.WriteUInt32(ApplyMode);
            writer.WriteUInt32(TEXTURE_COUNT);
            foreach (var texture in Textures)
            {
                writer.WriteBool(texture is not null);
                texture?.Write(writer);
            }
        }
    }
}
