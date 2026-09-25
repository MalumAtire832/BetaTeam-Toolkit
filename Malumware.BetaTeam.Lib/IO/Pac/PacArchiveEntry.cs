namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public class PacArchiveEntry
    {
        /// <summary>Separator between sub-directory names in <see cref="FileName"/>, as used by the game.</summary>
        public const char PATH_SEPARATOR = '\\';

        /// <summary>Path of the file within the archive, e.g. <c>FOO.TGA</c> or <c>SUBDIR\FOO.TGA</c>.</summary>
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

        /// <summary>The <see cref="FileName"/> converted to a relative path for the current operating system.</summary>
        public string ToLocalPath()
        {
            return FileName.Replace(PATH_SEPARATOR, Path.DirectorySeparatorChar);
        }
    }
}
