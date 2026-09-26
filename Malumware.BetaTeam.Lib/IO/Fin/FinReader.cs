namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public class FinReader
    {
        public FinFile Read(string filePath)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var bytes = File.ReadAllBytes(filePath);

            return Read(name, bytes);
        }

        internal static FinFile Read(string name, byte[] bytes, FinBlockRegistry? registry = null)
        {
            FinHeader header;
            using (var headerParser = new FinHeaderParser(new MemoryStream(bytes)))
            {
                header = headerParser.Parse();
            }

            using (var streamParser = new FinStreamParser(new MemoryStream(bytes), name, header, registry ?? FinBlockRegistry.Default))
            {
                streamParser.Seek(header.Size, SeekOrigin.Begin);
                return streamParser.Parse();
            }
        }
    }
}
