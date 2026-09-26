using System.Text;
using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public class FinHeaderParser : AbstractParser<FinHeader>
    {
        public FinHeaderParser(Stream stream)
            : base(stream) { }

        public override FinHeader Parse()
        {
            var line = ReadLine();
            if (!line.StartsWith(FinHeader.PREFIX, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Not a FIN file: the header doesn't start with \"Dweezil \"");
            }

            // Like the engine, only digits may follow the prefix
            var digits = line[FinHeader.PREFIX.Length..];
            if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
            {
                throw new InvalidDataException($"Not a FIN file: invalid version \"{digits}\"");
            }

            if (!int.TryParse(digits, out var version))
            {
                throw new InvalidDataException($"Not a FIN file: version \"{digits}\" is out of range");
            }

            var header = new FinHeader(version, line.Length + 1);
            if (!header.IsValid)
            {
                throw new InvalidDataException(
                    $"Unsupported FIN version {header.Version}, expected {FinHeader.SUPPORTED_VERSION}"
                );
            }

            return header;
        }

        private string ReadLine()
        {
            var bytes = new List<byte>();
            while (bytes.Count < FinHeader.MAX_LINE_LENGTH)
            {
                if (Reader.BaseStream.Position >= Reader.BaseStream.Length)
                {
                    throw new InvalidDataException("Not a FIN file: the header line has no line feed");
                }

                var b = Reader.ReadByte();
                if (b == (byte)'\n')
                {
                    return Encoding.ASCII.GetString(bytes.ToArray());
                }
                bytes.Add(b);
            }

            throw new InvalidDataException(
                $"Not a FIN file: no line feed within the first {FinHeader.MAX_LINE_LENGTH} bytes"
            );
        }
    }
}
