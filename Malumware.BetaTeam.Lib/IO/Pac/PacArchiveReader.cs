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
            using var entryParser = new PacArchiveEntryParser(streamFactory());
            using var dataParser = new PacArchiveEntryDataParser(streamFactory());

            var header = headerParser.Parse();
            var entries = new Dictionary<PacArchiveEntry, byte[]>((int)header.FileCount);
            if (header.FileCount <= 0)
            {
                return new PacArchive(fileName, header, entries);
            }

            entryParser.Seek(PacArchiveHeader.SIZE, SeekOrigin.Begin);
            for (var i = 0; i < header.FileCount; i++)
            {
                var entry = entryParser.Parse();
                var data = dataParser.Parse(entry);
                entries.Add(entry, data);
            }

            return new PacArchive(fileName, header, entries);
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
                var destPath = Path.Combine(directory, entry.FileName);
                File.WriteAllBytes(destPath, data);

                if (entry.LastModified.HasValue)
                {
                    File.SetLastWriteTimeUtc(destPath, entry.LastModified.Value);
                }
            }
        }
    }
}
