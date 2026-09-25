namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // A texture: either the name of an image file or a link to pixel data stored in the scene
    public class NiImage : NiObject
    {
        public bool External { get; private set; }
        public string? FileName { get; private set; }
        public FinRef<NiObject> RawData { get; private set; } = new(0);
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
