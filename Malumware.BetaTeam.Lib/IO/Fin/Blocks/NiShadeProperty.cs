namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiShadeProperty : NiProperty
    {
        public bool Smooth { get; private set; }

        protected override bool HasMasterFlag => false;

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Smooth = reader.ReadByte() != 0;
        }
    }
}
