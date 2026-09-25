using System.Text;
using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Locale
{
    public class StringTableParser : AbstractParser<IReadOnlyList<StringTableEntry>>
    {
        private const string TAG_MARKER = "==";
        private const char COMMENT_CHAR = '#';
        private const int WINDOWS_1252 = 1252;

        private static readonly Encoding ENCODING = CreateEncoding();

        private readonly List<StringTableEntry> _entries = [];
        private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);
        private string? _currentKey;
        private readonly List<string> _currentLines = [];

        public StringTableParser(Stream stream) : base(stream)
        {
        }

        public override IReadOnlyList<StringTableEntry> Parse()
        {
            var line = new List<byte>();
            var escaped = false;
            int value;
            while ((value = ReadByte()) != -1)
            {
                // Carriage returns are dropped before escapes are handled, so a backslash before CRLF still joins lines
                if (value == '\r')
                {
                    continue;
                }

                if (escaped)
                {
                    escaped = false;
                    AppendEscape(line, value);
                    continue;
                }

                if (value == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (value == '\n')
                {
                    CompleteLine(Decode(line));
                    line.Clear();
                    continue;
                }

                line.Add((byte)value);
            }

            if (line.Count > 0)
            {
                CompleteLastLine(Decode(line));
            }

            FinishEntry();
            return _entries;
        }

        private void AppendEscape(List<byte> line, int value)
        {
            switch (value)
            {
                case '\n':
                    // A backslash at the end of a line joins it with the next one
                    break;
                case 'n':
                    line.Add((byte)'\n');
                    break;
                case 't':
                    line.Add((byte)'\t');
                    break;
                case 'x':
                case 'X':
                    var high = ReadHexDigit();
                    var low = ReadHexDigit();
                    line.Add((byte)(high * 16 + low));
                    break;
                default:
                    line.Add((byte)value);
                    break;
            }
        }

        // The game always consumes two characters and treats anything that isn't a hex digit as zero
        private int ReadHexDigit()
        {
            var value = ReadByte();
            return value switch
            {
                >= '0' and <= '9' => value - '0',
                >= 'A' and <= 'F' => value - 'A' + 10,
                >= 'a' and <= 'f' => value - 'a' + 10,
                _ => 0
            };
        }

        private int ReadByte()
        {
            return Reader.BaseStream.ReadByte();
        }

        private static string Decode(List<byte> line)
        {
            return ENCODING.GetString(line.ToArray());
        }

        private static Encoding CreateEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(WINDOWS_1252);
        }

        private void CompleteLine(string line)
        {
            // Any line starting with the marker ends the current value, even when it isn't a valid tag
            if (line.StartsWith(TAG_MARKER, StringComparison.Ordinal))
            {
                FinishEntry();
                StartEntry(line);
                return;
            }

            if (_currentKey is not null && (line.Length == 0 || line[0] != COMMENT_CHAR))
            {
                _currentLines.Add(line);
            }
        }

        // Tags are only recognised on complete lines, but the marker still ends the current value
        private void CompleteLastLine(string line)
        {
            if (line.StartsWith(TAG_MARKER, StringComparison.Ordinal))
            {
                FinishEntry();
                return;
            }

            CompleteLine(line);
        }

        private void StartEntry(string line)
        {
            // The game only accepts tag lines longer than four characters and ignores text after the closing marker
            var end = line.IndexOf(TAG_MARKER, TAG_MARKER.Length, StringComparison.Ordinal);
            if (line.Length <= TAG_MARKER.Length * 2 || end < 0)
            {
                return;
            }

            // Keys are case-insensitive and the first definition wins
            var key = line[TAG_MARKER.Length..end];
            if (_keys.Add(key))
            {
                _currentKey = key;
            }
        }

        private void FinishEntry()
        {
            if (_currentKey is not null)
            {
                _entries.Add(new StringTableEntry(_currentKey, string.Join("\n", _currentLines)));
            }

            _currentKey = null;
            _currentLines.Clear();
        }
    }
}
