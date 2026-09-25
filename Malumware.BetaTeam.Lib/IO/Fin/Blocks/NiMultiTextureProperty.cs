namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Several texture stages drawn on top of each other; each list has one entry per stage
    public class NiMultiTextureProperty : NiProperty
    {
        public IReadOnlyList<FinRef<NiImage>> Images { get; private set; } = [];
        public uint[] CombineModes { get; private set; } = [];
        public uint[] ClampModes { get; private set; } = [];
        public uint[] FilterModes { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Images = reader.ReadRefList<NiImage>();
            CombineModes = ReadValues(reader);
            ClampModes = ReadValues(reader);
            FilterModes = ReadValues(reader);
        }

        private static uint[] ReadValues(FinBlockReader reader)
        {
            var count = reader.ReadCount(sizeof(uint));
            return reader.ReadArray(count, r => r.ReadUInt32());
        }
    }
}
