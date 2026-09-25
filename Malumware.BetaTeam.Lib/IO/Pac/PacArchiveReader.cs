using System.IO.MemoryMappedFiles;

namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public class PacArchiveReader
    {
        public PacArchive Read(string filePath)
        {
            using var mmf = MemoryMappedFile.CreateFromFile(
                filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read
            );
            return Read(filePath, () => CreateStream(mmf));
        }

        internal PacArchive Read(string filePath, Func<Stream> streamFactory)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            
            using var headerParser = new PacArchiveHeaderParser(streamFactory());
            using var directoryParser = new PacArchiveDirectoryParser(streamFactory());
            using var dataParser = new PacArchiveEntryDataParser(streamFactory());

            var header = headerParser.Parse();

            directoryParser.Seek(PacArchiveHeader.SIZE, SeekOrigin.Begin);
            var directory = directoryParser.Parse();

            var entries = new Dictionary<PacArchiveEntry, byte[]>(directory.Count);
            foreach (var entry in directory)
            {
                if (IsSelfReference(entry, filePath))
                {
                    continue;
                }

                var data = dataParser.Parse(entry);
                entries.Add(entry, data);
            }

            return new PacArchive(fileName, header, entries);
        }

        // The original packing tool recorded some archives inside themselves as an empty entry,
        // most likely because the half-written output file was sitting in the directory being packed.
        private static bool IsSelfReference(PacArchiveEntry entry, string filePath)
        {
            return entry.Size == 0
                && string.Equals(entry.FileName, Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase);
        }

        private static MemoryMappedViewStream CreateStream(MemoryMappedFile mmf)
        {
            return mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        }

        public void Unpack(PacArchive archive, string directory)
        {
            Directory.CreateDirectory(directory);

            foreach (var (entry, data) in archive.Entries)
            {
                var destPath = entry.GetDestinationPath(directory);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.WriteAllBytes(destPath, data);

                if (entry.LastModified.HasValue)
                {
                    File.SetLastWriteTimeUtc(destPath, entry.LastModified.Value);
                }
            }
        }
    }
}
