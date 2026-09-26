namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Extra data is stored inline in its owner, not as a separate block
    public class NiExtraData
    {
        public string ClassName { get; internal set; } = "";
        public long Offset { get; internal set; }
        public uint Size { get; private set; }

        // Only plain NiExtraData stores its bytes; subclasses save a size but read their own fields instead
        public byte[]? Data { get; private set; }

        internal virtual void Load(FinBlockReader reader)
        {
            Size = reader.ReadUInt32();
            if (GetType() == typeof(NiExtraData) && Size > 0)
            {
                if (Size > reader.Length - reader.Position)
                {
                    throw new InvalidDataException($"Extra data of {Size} bytes at offset 0x{Offset:X} exceeds the file");
                }
                Data = reader.ReadArray((int)Size, r => r.ReadByte());
            }
        }
    }
}
