namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public abstract class NiGeometry : NiAVObject
    {
        public NiGeometryData? Data { get; set; }

        internal override IEnumerable<NiObject?> GetLinks()
        {
            return base
                .GetLinks()
                .Append(Data);
        }

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteRef(Data);
            // Skin instance: skinning isn't exported
            writer.WriteRef(null);
        }
    }
}
