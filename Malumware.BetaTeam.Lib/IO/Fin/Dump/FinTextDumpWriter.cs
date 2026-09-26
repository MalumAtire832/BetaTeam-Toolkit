namespace Malumware.BetaTeam.Lib.IO.Fin.Dump
{
    // Writes the dump as a plain text tree. Data arrays and lists are summarised unless full is set.
    public static class FinTextDumpWriter
    {
        private const string BRANCH = "├─ ";
        private const string LAST_BRANCH = "└─ ";
        private const string PIPE = "│  ";
        private const string SPACE = "   ";

        public static void Write(FinDump dump, TextWriter writer, bool full)
        {
            WriteLine(writer, $"{dump.Name} (FIN version {dump.Version})");
            foreach (var root in dump.Roots)
            {
                WriteLine(writer, Header(root));
                WriteChildren(writer, root.Children, "", full);
            }

            if (dump.Unreferenced.Count > 0)
            {
                WriteLine(writer, "Unreferenced");
                WriteChildren(writer, dump.Unreferenced, "", full);
            }
        }

        private static void WriteChildren(TextWriter writer, IReadOnlyList<FinDumpNode> children, string indent, bool full, bool indexed = false)
        {
            for (var i = 0; i < children.Count; i++)
            {
                var last = i == children.Count - 1;
                var label = indexed ? $"[{i}]" : null;
                WriteNode(writer, children[i], label, indent + (last ? LAST_BRANCH : BRANCH), indent + (last ? SPACE : PIPE), full);
            }
        }

        private static void WriteNode(TextWriter writer, FinDumpNode node, string? label, string prefix, string childIndent, bool full)
        {
            // Fields read "Name: value", list items "[0] value"
            var name = node.Field ?? label;
            var lead = node.Field is not null ? $"{node.Field}: " : label is not null ? $"{label} " : "";

            switch (node)
            {
                case FinDumpObject obj:
                    WriteLine(writer, prefix + lead + Header(obj));
                    WriteChildren(writer, obj.Children, childIndent, full);
                    break;
                case FinDumpReference reference:
                    WriteLine(writer, $"{prefix}{lead}→ {reference.ClassName} @0x{reference.LinkId:X8}");
                    break;
                case FinDumpValue value:
                    WriteLine(writer, prefix + lead + FinDumpValueFormatter.Format(value.Value));
                    break;
                case FinDumpList list when list.Items.Count == 0:
                case FinDumpArray { Values.Count: 0 }:
                    WriteLine(writer, $"{prefix}{lead}[]");
                    break;
                case FinDumpList list when list.IsData && !full:
                    WriteLine(writer, $"{prefix}{lead}{list.Items.Count} × {list.ElementType}");
                    break;
                case FinDumpList list:
                    WriteLine(writer, prefix + name);
                    WriteChildren(writer, list.Items, childIndent, full, indexed: true);
                    break;
                case FinDumpArray array when !full:
                    WriteLine(writer, $"{prefix}{lead}{array.Values.Count} × {array.ElementType}");
                    break;
                case FinDumpArray array:
                    WriteLine(writer, prefix + name);
                    var values = array.Values
                        .Select(v => (FinDumpNode)new FinDumpValue(null, v))
                        .ToList();
                    WriteChildren(writer, values, childIndent, full, indexed: true);
                    break;
            }
        }

        private static string Header(FinDumpObject obj)
        {
            var header = obj.ClassName;
            if (obj.LinkId is { } linkId)
            {
                header += $" @0x{linkId:X8}";
            }
            if (obj.Offset is { } offset)
            {
                header += $" [offset 0x{offset:X}]";
            }

            return header;
        }

        // Always \n, whatever the platform, so dumps compare equal everywhere
        private static void WriteLine(TextWriter writer, string line)
        {
            writer.Write(line);
            writer.Write('\n');
        }
    }
}
