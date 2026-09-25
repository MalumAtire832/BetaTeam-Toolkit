namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Extra data is stored inline in its owner, not as a separate block
    public abstract class NiExtraData
    {
        public string ClassName { get; internal set; } = "";
        public long Offset { get; internal set; }

        internal virtual void Load(FinBlockReader reader) { }
    }
}
