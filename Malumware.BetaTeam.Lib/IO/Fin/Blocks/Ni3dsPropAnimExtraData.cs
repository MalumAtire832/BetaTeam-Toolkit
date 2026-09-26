namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Attached to a property to point at the 3ds animator that animates it
    public class Ni3dsPropAnimExtraData : NiExtraData
    {
        public FinRef<NiObject> Animator { get; private set; } = new(0);

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Animator = reader.ReadRef<NiObject>();
        }
    }
}
