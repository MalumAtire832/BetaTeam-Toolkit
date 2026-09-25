using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public record PacArchiveHeader
    {
        /// <summary>The correct "magic" value at the start of each PACK archive</summary>
        public const string MARKER = "PACK";

        /// <summary>Total size in bytes of the fixed header at the start of a PACK archive.</summary>
        public const int SIZE = 16;

        public byte[] Magic { get; }
        public uint ArchiveSize { get; }
        public uint DirectorySize { get; }

        public bool IsValid => Encoding.ASCII.GetString(Magic) == MARKER;

        public PacArchiveHeader(byte[] magic, uint archiveSize, uint directorySize)
        {
            Magic = magic;
            ArchiveSize = archiveSize;
            DirectorySize = directorySize;
        }
    }
}
