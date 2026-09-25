namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiSpecularProperty : NiProperty
    {
        public bool Specular { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Specular = reader.ReadByte() != 0;
        }
    }
}
