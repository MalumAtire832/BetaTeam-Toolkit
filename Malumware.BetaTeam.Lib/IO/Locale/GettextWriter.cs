using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Locale
{
    public static class GettextWriter
    {
        public static string ToPo(IReadOnlyList<StringTable> tables, string language)
        {
            return Write(tables, language);
        }

        public static string ToPot(IReadOnlyList<StringTable> tables)
        {
            return Write(tables, null);
        }

        private static string Write(IReadOnlyList<StringTable> tables, string? language)
        {
            var builder = new StringBuilder();
            builder.Append("msgid \"\"\nmsgstr \"\"\n");
            builder.Append("\"Content-Type: text/plain; charset=UTF-8\\n\"\n");
            builder.Append("\"Content-Transfer-Encoding: 8bit\\n\"\n");
            if (language != null)
            {
                builder.Append("\"Language: ").Append(Escape(language)).Append("\\n\"\n");
            }

            // The first definition of a key wins, as in the engine, and a PO file can't repeat a msgid
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in tables)
            {
                foreach (var entry in table.Entries)
                {
                    if (!written.Add(entry.Key))
                    {
                        continue;
                    }

                    CheckText(table.Name, entry);
                    builder.Append("\n#. ").Append(table.Name).Append('\n');
                    builder.Append("msgid ").Append(Quote(entry.Key.ToUpperInvariant())).Append('\n');
                    builder.Append("msgstr ");
                    if (language == null)
                    {
                        builder.Append("\"\"\n");
                    }
                    else
                    {
                        AppendMsgstr(builder, entry.Text.TrimEnd('\n'));
                    }
                }
            }

            return builder.ToString();
        }

        private static void AppendMsgstr(StringBuilder builder, string text)
        {
            var lines = text.Split('\n');
            if (lines.Length == 1)
            {
                builder.Append(Quote(text)).Append('\n');

                return;
            }

            builder.Append("\"\"\n");
            for (var i = 0; i < lines.Length; i++)
            {
                var last = i == lines.Length - 1;
                builder.Append('"').Append(Escape(lines[i]));
                builder.Append(last ? "\"\n" : "\\n\"\n");
            }
        }

        private static void CheckText(string table, StringTableEntry entry)
        {
            foreach (var c in entry.Text)
            {
                if (char.IsControl(c) && c != '\n' && c != '\t')
                {
                    throw new InvalidDataException(
                        $"Table {table}, key {entry.Key}: control character U+{(int)c:X4} can't be written to a PO file");
                }
            }
        }

        private static string Quote(string text)
        {
            return $"\"{Escape(text)}\"";
        }

        private static string Escape(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\t", "\\t");
        }
    }
}
