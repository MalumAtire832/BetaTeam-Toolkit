namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public class PacArchive
    {
        public string FileName { get; }

        public PacArchiveHeader Header { get; }
        public IReadOnlyDictionary<PacArchiveEntry, byte[]> Entries { get; }

        public PacArchive(
            string fileName,
            PacArchiveHeader header,
            IReadOnlyDictionary<PacArchiveEntry, byte[]> entries)
        {
            FileName = fileName;
            Header = header;
            Entries = entries;
        }
    }
}