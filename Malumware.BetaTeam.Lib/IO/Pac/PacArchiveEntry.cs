namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public class PacArchiveEntry
    {
        public const char PATH_SEPARATOR = '\\';

        public string FileName { get; }
        public long Offset { get; }
        public int Size { get; }
        public DateTime? LastModified { get; }

        public PacArchiveEntry(string fileName, long offset, int size, DateTime? lastModified)
        {
            FileName = fileName;
            Offset = offset;
            Size = size;
            LastModified = lastModified;
        }

        public string ToLocalPath()
        {
            return FileName.Replace(PATH_SEPARATOR, Path.DirectorySeparatorChar);
        }
    }
}
