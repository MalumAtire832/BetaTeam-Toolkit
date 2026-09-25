using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    internal sealed class TestBlock : NiObject
    {
        public uint Value { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Value = reader.ReadUInt32();
        }
    }

    internal sealed class TestParentBlock : NiObject
    {
        public FinRef<TestBlock> Child { get; private set; } = null!;

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Child = reader.ReadRef<TestBlock>();
        }
    }

    internal sealed class TestExtraData : NiExtraData
    {
        public uint Value { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            Value = reader.ReadUInt32();
        }
    }

    internal static class TestRegistry
    {
        public static FinBlockRegistry Create()
        {
            return new FinBlockRegistry()
                .RegisterBlock<TestBlock>()
                .RegisterBlock<TestParentBlock>()
                .RegisterExtraData<TestExtraData>();
        }
    }
}
