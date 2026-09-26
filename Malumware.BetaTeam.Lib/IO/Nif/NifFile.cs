using Malumware.BetaTeam.Lib.IO.Nif.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Nif
{
    public class NifFile
    {
        public IReadOnlyList<NiObject> Roots { get; }

        public NifFile(IReadOnlyList<NiObject> roots)
        {
            Roots = roots;
        }

        // Every block reachable from the roots, parents before the blocks they link to. Blocks shared by several
        // parents appear once, at their first use.
        public IReadOnlyList<NiObject> CollectBlocks()
        {
            var blocks = new List<NiObject>();
            var visited = new HashSet<NiObject>(ReferenceEqualityComparer.Instance);
            var stack = new Stack<NiObject>();
            for (var i = Roots.Count - 1; i >= 0; i--)
            {
                stack.Push(Roots[i]);
            }

            // An explicit stack, so a deep scene graph can't overflow the call stack
            while (stack.Count > 0)
            {
                var block = stack.Pop();
                if (!visited.Add(block))
                {
                    continue;
                }

                blocks.Add(block);
                var links = block
                    .GetLinks()
                    .OfType<NiObject>()
                    .ToList();
                for (var i = links.Count - 1; i >= 0; i--)
                {
                    stack.Push(links[i]);
                }
            }

            return blocks;
        }
    }
}
