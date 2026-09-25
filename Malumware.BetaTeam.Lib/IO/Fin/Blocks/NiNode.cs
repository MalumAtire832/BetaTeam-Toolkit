namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiNode : NiAVObject
    {
        public NiSortingMode SortingMode { get; private set; }

        // The sorter's memory address at save time. The engine reads it back but never links it, so it means nothing.
        public uint Sorter { get; private set; }

        public bool IsVisualObject { get; private set; }

        // Empty slots are stored as link ID 0
        public IReadOnlyList<FinRef<NiAVObject>> Children { get; private set; } = [];
        public IReadOnlyList<FinRef<NiObject>> Effects { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            SortingMode = (NiSortingMode)reader.ReadUInt32();
            Sorter = reader.ReadUInt32();
            IsVisualObject = reader.ReadByte() != 0;
            Children = reader.ReadRefList<NiAVObject>();
            Effects = reader.ReadRefList<NiObject>();
        }
    }
}
