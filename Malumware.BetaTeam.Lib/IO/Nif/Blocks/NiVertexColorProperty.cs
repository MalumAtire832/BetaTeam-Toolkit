namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiVertexColorProperty : NiProperty
    {
        // NIF's defaults, used by readers when a shape has no vertex color property
        public const uint VERTEX_MODE_AMBIENT_DIFFUSE = 2;
        public const uint LIGHTING_MODE_EMISSIVE_AMBIENT_DIFFUSE = 1;

        public ushort Flags { get; set; }
        public uint VertexMode { get; set; } = VERTEX_MODE_AMBIENT_DIFFUSE;
        public uint LightingMode { get; set; } = LIGHTING_MODE_EMISSIVE_AMBIENT_DIFFUSE;

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteUInt16(Flags);
            writer.WriteUInt32(VertexMode);
            writer.WriteUInt32(LightingMode);
        }
    }
}
