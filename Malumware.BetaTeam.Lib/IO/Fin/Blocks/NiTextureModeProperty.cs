namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // How the texture from the NiTextureProperty is applied, filtered and wrapped (engine: GetApply, GetFilter,
    // GetClamp; the enum values aren't mapped yet)
    public class NiTextureModeProperty : NiProperty
    {
        public uint ApplyMode { get; private set; }
        public uint FilterMode { get; private set; }
        public uint ClampMode { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            ApplyMode = reader.ReadUInt32();
            FilterMode = reader.ReadUInt32();
            ClampMode = reader.ReadUInt32();
        }
    }
}
