using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Parsers
{
    public abstract class AbstractParser<T> : IParser<T>
    {
        protected readonly BinaryReader Reader;

        protected AbstractParser(Stream stream)
        {
            Reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            Reader.BaseStream.Seek(offset, origin);
        }

        public abstract T Parse();

        public void Dispose()
        {
            Reader.Dispose();
        }
    }
}