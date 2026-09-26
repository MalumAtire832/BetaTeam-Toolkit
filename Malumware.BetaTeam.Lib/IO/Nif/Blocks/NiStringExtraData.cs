using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public class NiStringExtraData : NiExtraData
    {
        public string Value { get; set; }

        public NiStringExtraData(string value)
        {
            Value = value;
        }

        protected override uint ByteCount => (uint)(sizeof(uint) + Encoding.Latin1.GetByteCount(Value));

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteSizedString(Value);
        }
    }
}
