using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // A reference to another block, stored in the file as that block's link ID (its original memory address)
    public abstract class FinRef
    {
        public uint LinkId { get; }
        public NiObject? Target { get; private set; }
        public bool IsNull => LinkId == 0;
        public abstract Type TargetType { get; }

        protected FinRef(uint linkId)
        {
            LinkId = linkId;
        }

        internal void Resolve(NiObject target)
        {
            Target = target;
        }
    }

    public sealed class FinRef<T> : FinRef
        where T : NiObject
    {
        public new T? Target => (T?)base.Target;
        public override Type TargetType => typeof(T);

        public FinRef(uint linkId)
            : base(linkId) { }
    }
}
