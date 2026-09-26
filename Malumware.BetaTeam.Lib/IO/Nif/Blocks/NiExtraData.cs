namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public abstract class NiExtraData : NiObject
    {
        public NiExtraData? NextExtraData { get; internal set; }

        // The size of the subclass's data after this field. Readers ignore it, but it is kept accurate.
        protected abstract uint ByteCount { get; }

        internal override IEnumerable<NiObject?> GetLinks()
        {
            return [NextExtraData];
        }

        internal override void Write(NifWriter writer)
        {
            writer.WriteRef(NextExtraData);
            writer.WriteUInt32(ByteCount);
        }
    }
}
