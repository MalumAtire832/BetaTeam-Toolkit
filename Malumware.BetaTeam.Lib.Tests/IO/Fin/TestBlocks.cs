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

    internal sealed class TestListBlock : NiObject
    {
        public IReadOnlyList<FinRef<NiObject>> Items { get; private set; } = [];
        public float[] Values { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Items = reader.ReadRefList<NiObject>();
            var count = reader.ReadCount(sizeof(float));
            Values = reader.ReadArray(count, r => r.ReadSingle());
        }
    }

    internal sealed record TestData(FinRef<NiObject> Link, float X);

    internal sealed class TestDataBlock : NiObject
    {
        public TestData Data { get; private set; } = null!;
        public TestData[] Entries { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Data = new TestData(reader.ReadRef<NiObject>(), reader.ReadSingle());
            var count = reader.ReadCount(8);
            Entries = reader.ReadArray(count, r => new TestData(r.ReadRef<NiObject>(), r.ReadSingle()));
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
                .RegisterBlock<TestListBlock>()
                .RegisterBlock<TestDataBlock>()
                .RegisterExtraData<TestExtraData>();
        }
    }
}
