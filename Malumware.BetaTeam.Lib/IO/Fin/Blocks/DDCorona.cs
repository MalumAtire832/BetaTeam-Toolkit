namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // A light glow that faces the camera and fades with distance or when something solid is in the way. The engine
    // builds its triangles when drawing, so none are stored.
    public class DDCorona : NiTriBasedGeom
    {
        // Size of the glow; zero or less means the object's scale is used instead
        public float Size { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Size = reader.ReadSingle();
        }
    }
}
