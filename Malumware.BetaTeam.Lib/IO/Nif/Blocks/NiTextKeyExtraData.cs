using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    // Named points on the animation timeline, such as the start and end of each animation clip
    public class NiTextKeyExtraData : NiExtraData
    {
        public List<NifTextKey> TextKeys { get; } = [];

        protected override uint ByteCount
        {
            get
            {
                var count = sizeof(uint);
                foreach (var key in TextKeys)
                {
                    count += sizeof(float) + sizeof(uint) + Encoding.Latin1.GetByteCount(key.Value);
                }

                return (uint)count;
            }
        }

        internal override void Write(NifWriter writer)
        {
            base.Write(writer);
            writer.WriteCount(TextKeys.Count);
            foreach (var key in TextKeys)
            {
                writer.WriteSingle(key.Time);
                writer.WriteSizedString(key.Value);
            }
        }
    }
}
