namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Turns to face the camera
    public class NiBillboardNode : NiNode
    {
        // Engine: SetMode; the values aren't mapped yet
        public int Mode { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Mode = reader.ReadInt32();
        }
    }
}
