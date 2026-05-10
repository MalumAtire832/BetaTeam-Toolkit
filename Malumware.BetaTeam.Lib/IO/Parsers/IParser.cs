namespace Malumware.BetaTeam.Lib.IO.Parsers
{
    public interface IParser<out T> : IDisposable
    {
        public T Parse();
    }
}