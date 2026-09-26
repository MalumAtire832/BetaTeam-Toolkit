namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiMaterialProperty : NiProperty
    {
        public FinColor3 AmbientColor { get; private set; }
        public FinColor3 DiffuseColor { get; private set; }
        public FinColor3 SpecularColor { get; private set; }
        public FinColor3 Emittance { get; private set; }
        // The engine spells it GetShineness
        public float Shininess { get; private set; }
        public float Alpha { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            AmbientColor = reader.ReadColor3();
            DiffuseColor = reader.ReadColor3();
            SpecularColor = reader.ReadColor3();
            Emittance = reader.ReadColor3();
            Shininess = reader.ReadSingle();
            Alpha = reader.ReadSingle();
        }
    }
}
