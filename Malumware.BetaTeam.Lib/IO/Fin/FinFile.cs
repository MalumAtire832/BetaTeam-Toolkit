using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public class FinFile
    {
        public string Name { get; }
        public FinHeader Header { get; }
        public IReadOnlyList<NiObject> Objects { get; }
        public IReadOnlyList<NiObject> TopLevelObjects { get; }

        public FinFile(
            string name,
            FinHeader header,
            IReadOnlyList<NiObject> objects,
            IReadOnlyList<NiObject> topLevelObjects)
        {
            Name = name;
            Header = header;
            Objects = objects;
            TopLevelObjects = topLevelObjects;
        }
    }
}
