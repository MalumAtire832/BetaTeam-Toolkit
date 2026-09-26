namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Render state (material, texture, blending, ...) that applies to an object and everything below it
    public abstract class NiProperty : NiObject
    {
        // Engine: GetMaster; its effect isn't confirmed
        public bool Master { get; private set; }

        // NiShadeProperty's loader skips NiProperty and goes straight to NiObject, so it has no master flag
        protected virtual bool HasMasterFlag => true;

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            if (HasMasterFlag)
            {
                Master = reader.ReadByte() != 0;
            }
        }
    }
}
