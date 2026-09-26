using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Animates a colour; each key's value holds red, green and blue
    public class Ni3dsColorAnimator : Ni3dsLightAnimator
    {
        public uint Unknown44 { get; private set; }
        public FinKeyGroup<FinPosKey> Keys { get; private set; } = FinKeyGroup<FinPosKey>.Empty;

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Unknown44 = reader.ReadUInt32();
            Keys = FinKeyReader.ReadPosKeys(reader);
        }
    }
}
