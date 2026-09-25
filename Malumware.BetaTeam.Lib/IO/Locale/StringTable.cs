using System.Diagnostics.CodeAnalysis;

namespace Malumware.BetaTeam.Lib.IO.Locale
{
    public class StringTable
    {
        public string Name { get; }
        public IReadOnlyList<StringTableEntry> Entries { get; }

        private readonly Dictionary<string, string> _texts = new(StringComparer.OrdinalIgnoreCase);

        public StringTable(string name, IReadOnlyList<StringTableEntry> entries)
        {
            Name = name;
            Entries = entries;
            foreach (var entry in entries)
            {
                // Like the game, the first definition of a key wins
                _texts.TryAdd(entry.Key, entry.Text);
            }
        }

        public bool TryGetText(string key, [NotNullWhen(true)] out string? text)
        {
            return _texts.TryGetValue(key, out text);
        }
    }
}
