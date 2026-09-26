using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // A node animated with keyframes exported from 3ds Max
    public class Ni3dsAnimationNode : NiNode
    {
        public FinAnimationCore Core { get; private set; } = null!;
        public FinKeyGroup<FinRotKey> RotationKeys { get; private set; } = FinKeyGroup<FinRotKey>.Empty;
        public FinKeyGroup<FinPosKey> PositionKeys { get; private set; } = FinKeyGroup<FinPosKey>.Empty;
        public FinKeyGroup<FinFloatKey> ScaleKeys { get; private set; } = FinKeyGroup<FinFloatKey>.Empty;
        public FinVisKey[] VisibilityKeys { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Core = FinAnimationCore.Read(reader);
            RotationKeys = FinKeyReader.ReadOptionalRotKeys(reader);
            PositionKeys = FinKeyReader.ReadOptionalPosKeys(reader);
            ScaleKeys = FinKeyReader.ReadOptionalFloatKeys(reader);
            var count = reader.ReadSignedCount(5);
            VisibilityKeys = reader.ReadArray(count, r => new FinVisKey(r.ReadSingle(), r.ReadByte() != 0));
        }
    }
}
