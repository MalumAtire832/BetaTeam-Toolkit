using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Parsers
{
    public abstract class LineParser<T> : IParser<T>
    {
        protected readonly StreamReader Reader;

        protected LineParser(Stream stream)
        {
            Reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: false);
        }

        public abstract T Parse();

        public void Dispose()
        {
            Reader.Dispose();
        }
    }
}