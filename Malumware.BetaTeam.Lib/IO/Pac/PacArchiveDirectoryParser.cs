using System.Text;
using Malumware.BetaTeam.Lib.IO.Parsers;
using Malumware.Common;

namespace Malumware.BetaTeam.Lib.IO.Pac
{
    public class PacArchiveDirectoryParser : AbstractParser<IReadOnlyList<PacArchiveEntry>>
    {
        public const int MAX_NAME_LENGTH = 63;

        public PacArchiveDirectoryParser(Stream stream)
            : base(stream) { }

        public override IReadOnlyList<PacArchiveEntry> Parse()
        {
            var entries = new List<PacArchiveEntry>();
            ParseDirectory(string.Empty, entries);

            return entries;
        }

        private void ParseDirectory(string pathPrefix, List<PacArchiveEntry> entries)
        {
            var fileCount = Reader.ReadUInt32();
            for (var i = 0; i < fileCount; i++)
            {
                entries.Add(ParseEntry(pathPrefix));
            }

            var subDirectoryCount = Reader.ReadUInt32();
            for (var i = 0; i < subDirectoryCount; i++)
            {
                var directoryName = ReadName();
                ParseDirectory(pathPrefix + directoryName + PacArchiveEntry.PATH_SEPARATOR, entries);
            }
        }

        private PacArchiveEntry ParseEntry(string pathPrefix)
        {
            var fileName = ReadName();
            var offsetHigh = Reader.ReadUInt32(); // Stored high dword first, as passed to MapViewOfFile
            var offsetLow = Reader.ReadUInt32();
            var fileSize = Reader.ReadUInt32();
            var fileModifyTime = ReadModifyTime();

            var offset = ((long)offsetHigh << 32) | offsetLow;

            return new PacArchiveEntry(
                pathPrefix + fileName, offset, (int)fileSize, fileModifyTime
            );
        }

        private string ReadName()
        {
            var bytes = Reader.ReadBytesUntil(0, 20);

            if (bytes.Length > MAX_NAME_LENGTH)
            {
                throw new InvalidDataException(
                    $"Name exceeds {MAX_NAME_LENGTH} characters"
                );
            }

            var name = Encoding.ASCII.GetString(bytes);

            // The game splits paths on these characters, so a name containing one can never be looked up.
            // Rejecting them also prevents unpacking from escaping the output directory.
            if (name is "" or "." or ".." || name.IndexOfAny(['\\', '/', ':']) >= 0)
            {
                throw new InvalidDataException(
                    $"Invalid name in archive directory: '{name}'"
                );
            }

            return name;
        }

        private DateTime? ReadModifyTime()
        {
            var raw = Reader.ReadInt64();

            if (raw <= 0)
            {
                return null;
            }

            try
            {
                var parsed = DateTime.FromFileTimeUtc(raw);
                return parsed;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }
}
