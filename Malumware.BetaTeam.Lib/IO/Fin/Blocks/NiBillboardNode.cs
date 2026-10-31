namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Turns to face the camera
    public class NiBillboardNode : NiNode
    {
        // Engine: SetMode. 0 faces the camera, 1 turns about the parent's Y axis, 2 takes the camera's rotation
        public int Mode { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Mode = reader.ReadInt32();
        }
    }
}
