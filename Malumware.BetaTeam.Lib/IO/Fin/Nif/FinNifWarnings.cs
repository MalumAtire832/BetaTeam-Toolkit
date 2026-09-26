namespace Malumware.BetaTeam.Lib.IO.Fin.Nif
{
    internal sealed class FinNifWarnings
    {
        private readonly List<string> _order = [];
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

        public void Add(string warning)
        {
            if (_counts.TryGetValue(warning, out var count))
            {
                _counts[warning] = count + 1;
                return;
            }
            _counts[warning] = 1;
            _order.Add(warning);
        }

        public IReadOnlyList<string> ToList()
        {
            return _order
                .Select(e => _counts[e] == 1 ? e : $"{e} ({_counts[e]}x)")
                .ToList();
        }
    }
}
