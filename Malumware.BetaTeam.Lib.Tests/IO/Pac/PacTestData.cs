using System.Text;

namespace Malumware.BetaTeam.Lib.Tests.IO.Pac
{
    internal static class PacTestData
    {
        public static void WriteHeader(BinaryWriter writer, uint archiveSize, uint directorySize)
        {
            writer.Write(Encoding.ASCII.GetBytes("PACK"));
            writer.Write(archiveSize);
            writer.Write(0u);             // unknown
            writer.Write(directorySize);
        }

        public static void WriteName(BinaryWriter writer, string name)
        {
            writer.Write(Encoding.ASCII.GetBytes(name));
            writer.Write((byte)0);        // null terminator
        }

        public static void WriteEntry(BinaryWriter writer, string name, uint offsetLow, uint size, long fileTime = 0, uint offsetHigh = 0)
        {
            WriteName(writer, name);
            writer.Write(offsetHigh);
            writer.Write(offsetLow);
            writer.Write(size);
            writer.Write(fileTime);
        }

        public static int EntrySize(string name)
        {
            return name.Length + 1 + 4 + 4 + 4 + 8;
        }
    }
}
