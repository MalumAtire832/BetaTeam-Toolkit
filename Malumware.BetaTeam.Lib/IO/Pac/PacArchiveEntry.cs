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

        public string GetDestinationPath(string directory)
        {
            var root = Path.GetFullPath(directory);
            var destination = Path.GetFullPath(Path.Combine(root, ToLocalPath()));

            if (!destination.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Entry '{FileName}' resolves outside of the output directory"
                );
            }

            return destination;
        }
    }
}
