namespace Malumware.BetaTeam.Lib.IO.Locale
{
    public class StringTableReader
    {
        public StringTable Read(string filePath)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            var bytes = File.ReadAllBytes(filePath);

            return Read(name, bytes);
        }

        internal static StringTable Read(string name, byte[] bytes)
        {
            using (var parser = new StringTableParser(new MemoryStream(bytes)))
            {
                return new StringTable(name, parser.Parse());
            }
        }
    }
}
