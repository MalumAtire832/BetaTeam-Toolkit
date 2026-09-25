namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public record FinHeader
    {
        // Digital Domain replaced the NetImmerse header line through NiStream::SetNewHeader
        public const string PREFIX = "Dweezil ";
        public const int SUPPORTED_VERSION = 23;

        // The engine reads the line into a 128-byte buffer
        public const int MAX_LINE_LENGTH = 128;

        public int Version { get; }
        public int Size { get; }

        // The engine accepts exactly one version and rejects older and later ones
        public bool IsValid => Version == SUPPORTED_VERSION;

        public FinHeader(int version, int size)
        {
            Version = version;
            Size = size;
        }
    }
}
