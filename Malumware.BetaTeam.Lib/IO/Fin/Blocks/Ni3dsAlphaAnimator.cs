using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Animates a transparency value
    public class Ni3dsAlphaAnimator : Ni3dsLightAnimator
    {
        public FinKeyGroup<FinFloatKey> Keys { get; private set; } = FinKeyGroup<FinFloatKey>.Empty;

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Keys = FinKeyReader.ReadFloatKeys(reader);
        }
    }
}
