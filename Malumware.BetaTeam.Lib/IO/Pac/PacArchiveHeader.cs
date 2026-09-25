using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public record PacArchiveHeader
    {
        /// <summary>The correct "magic" value at the start of each PACK archive</summary>
        public const string MARKER = "PACK";

        /// <summary>Total size in bytes of the fixed header at the start of a PACK archive. The directory follows directly after it.</summary>
        public const int SIZE = 16;

        public byte[] Magic { get; }

        /// <summary>Total size of the archive in bytes. Not used by the game.</summary>
        public uint ArchiveSize { get; }

        /// <summary>Offset at which the directory ends, measured from the start of the archive.</summary>
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
