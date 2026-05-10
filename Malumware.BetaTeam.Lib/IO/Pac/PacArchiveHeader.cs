using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public record PacArchiveHeader
    {
        /// <summary>The correct "magic" value at the start of each PACK archive</summary>
        public const string MARKER = "PACK";

        /// <summary>Total size in bytes of the fixed header at the start of a PACK archive.</summary>
        public const int SIZE = 20;

        public byte[] Magic { get; }
        public uint ArchiveSize { get; }
        public uint PayloadOffset { get; }
        public uint FileCount { get; }

        public bool IsValid => Encoding.ASCII.GetString(Magic) == MARKER;

        public PacArchiveHeader(byte[] magic, uint archiveSize, uint payloadOffset, uint fileCount)
        {
            Magic = magic;
            ArchiveSize = archiveSize;
            PayloadOffset = payloadOffset;
            FileCount = fileCount;
        }
    }
}