namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public abstract class NiObjectNET : NiObject
    {
        public string Name { get; set; } = "";

        // Stored as a chain: the object links to the first entry, each entry to the next
        public NiExtraData? ExtraData { get; private set; }

        public void AddExtraData(NiExtraData extraData)
        {
            if (ExtraData is null)
            {
                ExtraData = extraData;
                return;
            }

            var last = ExtraData;
            while (last.NextExtraData is not null)
            {
                last = last.NextExtraData;
            }
            last.NextExtraData = extraData;
        }

        public IEnumerable<NiExtraData> EnumerateExtraData()
        {
            for (var extraData = ExtraData; extraData is not null; extraData = extraData.NextExtraData)
            {
                yield return extraData;
            }
        }

        internal override IEnumerable<NiObject?> GetLinks()
        {
            return [ExtraData];
        }

        internal override void Write(NifWriter writer)
        {
            writer.WriteSizedString(Name);
            writer.WriteRef(ExtraData);
            // Controller: animation isn't exported
            writer.WriteRef(null);
        }
    }
}
