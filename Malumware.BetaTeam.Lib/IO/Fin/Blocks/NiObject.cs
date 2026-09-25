namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Unlike later NetImmerse versions, the name and extra data live on NiObject itself
    public abstract class NiObject
    {
        public string ClassName { get; internal set; } = "";
        public long Offset { get; internal set; }
        public uint LinkId { get; private set; }
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
