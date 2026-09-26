namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Unlike later NetImmerse versions, the name and extra data live on NiObject itself
    public abstract class NiObject
    {
        public string ClassName { get; internal set; } = "";
        public long Offset { get; internal set; }
        // The object's memory address when the file was saved; only used to link blocks
        public uint LinkId { get; private set; }

        // The name the object had in 3ds Max, such as "Box01"
        public string? Name { get; private set; }
        public IReadOnlyList<NiExtraData> ExtraData { get; private set; } = [];

        internal virtual void Load(FinBlockReader reader)
        {
            LinkId = reader.ReadUInt32();
            Name = reader.ReadCString();
            ExtraData = reader.ReadExtraDataList();
        }
    }
}
