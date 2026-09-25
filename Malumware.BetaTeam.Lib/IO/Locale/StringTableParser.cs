using System.Text;
using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Locale
{
    public class StringTableParser : AbstractParser<IReadOnlyList<StringTableEntry>>
    {
        private const string TAG_MARKER = "==";
        private const char COMMENT_CHAR = '#';
        private const int ENCODING = 1252; // Windows 1252

        private readonly List<StringTableEntry> _entries = [];
        private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);
        private string? _currentKey;
        private readonly List<string> _currentLines = [];

        public StringTableParser(Stream stream) : base(stream)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
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

            CloseEntry();
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

        private int ReadHexDigit()
        {
            // The game always consumes two characters and treats anything that isn't a hex digit as zero
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
            return Encoding
                .GetEncoding(ENCODING)
                .GetString(line.ToArray());
        }

        private void CompleteLine(string line)
        {
            // Any line starting with the marker ends the current value, even when it isn't a valid tag
            if (line.StartsWith(TAG_MARKER, StringComparison.Ordinal))
            {
                CloseEntry();
                OpenEntry(line);
                return;
            }

            if (_currentKey is not null && (line.Length == 0 || line[0] != COMMENT_CHAR))
            {
                _currentLines.Add(line);
            }
        }

        private void CompleteLastLine(string line)
        {
            // Tags are only recognized on complete lines, but the marker still ends the current value
            if (line.StartsWith(TAG_MARKER, StringComparison.Ordinal))
            {
                CloseEntry();
                return;
            }

            CompleteLine(line);
        }

        private void OpenEntry(string line)
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

        private void CloseEntry()
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
