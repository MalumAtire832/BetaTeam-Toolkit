using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public sealed class FinBlockRegistry
    {
        public static FinBlockRegistry Default { get; } = CreateDefault();

        private readonly Dictionary<string, Func<NiObject>> _blocks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<NiExtraData>> _extraData = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> BlockClassNames => _blocks.Keys;
        public IReadOnlyCollection<string> ExtraDataClassNames => _extraData.Keys;

        // Block classes are named after the engine classes, so the C# name is the name in the file
        public FinBlockRegistry RegisterBlock<T>() where T : NiObject, new()
        {
            _blocks[typeof(T).Name] = () => new T();
            return this;
        }

        public FinBlockRegistry RegisterExtraData<T>() where T : NiExtraData, new()
        {
            _extraData[typeof(T).Name] = () => new T();
            return this;
        }

        internal NiObject? CreateBlock(string className)
        {
            return _blocks.TryGetValue(className, out var create) ? create() : null;
        }

        internal NiExtraData? CreateExtraData(string className)
        {
            return _extraData.TryGetValue(className, out var create) ? create() : null;
        }

        private static FinBlockRegistry CreateDefault()
        {
            return new FinBlockRegistry()
                .RegisterBlock<NiNode>()
                .RegisterBlock<NiTriShape>()
                .RegisterBlock<NiEnvMappedTriShape>()
                .RegisterBlock<NiLODNode>()
                .RegisterBlock<NiBillboardNode>()
                .RegisterBlock<NiLight>();
        }
    }
}
