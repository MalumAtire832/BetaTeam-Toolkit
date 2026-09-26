namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // A texture: either the name of an image file or a link to pixel data stored in the scene
    public class NiImage : NiObject
    {
        public bool External { get; private set; }
        // A bare file name; the engine resolves it against its texture folders when loading
        public string? FileName { get; private set; }
        public FinRef<NiObject> RawData { get; private set; } = new(0);
        // Engine: GetPreferredTextureFormat
        public uint PreferredTextureFormat { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            External = reader.ReadByte() != 0;
            if (External)
            {
                FileName = reader.ReadCString();
            }
            else
            {
                RawData = reader.ReadRef<NiObject>();
            }
            PreferredTextureFormat = reader.ReadUInt32();
        }
    }
}
