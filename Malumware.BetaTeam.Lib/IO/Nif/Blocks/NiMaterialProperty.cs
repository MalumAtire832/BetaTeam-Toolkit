namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiMaterialProperty : NiProperty
    {
        public ushort Flags { get; set; }
        public NifColor3 AmbientColor { get; set; } = new(1, 1, 1);
        public NifColor3 DiffuseColor { get; set; } = new(1, 1, 1);
        public NifColor3 SpecularColor { get; set; } = new(1, 1, 1);
        public NifColor3 EmissiveColor { get; set; }
        public float Glossiness { get; set; } = 10;
        public float Alpha { get; set; } = 1;

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteUInt16(Flags);
            writer.WriteColor3(AmbientColor);
            writer.WriteColor3(DiffuseColor);
            writer.WriteColor3(SpecularColor);
            writer.WriteColor3(EmissiveColor);
            writer.WriteSingle(Glossiness);
            writer.WriteSingle(Alpha);
        }
    }
}
