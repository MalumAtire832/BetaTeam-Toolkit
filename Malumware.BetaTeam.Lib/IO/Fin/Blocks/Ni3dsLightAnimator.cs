using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Base of the 3ds animators that change a light or property over time
    public abstract class Ni3dsLightAnimator : NiObject
    {
        public FinAnimationCore Core { get; private set; } = null!;
        public FinRef<NiObject> Target { get; private set; } = new(0);

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Core = FinAnimationCore.Read(reader);
            Target = reader.ReadRef<NiObject>();
        }
    }
}
