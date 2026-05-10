namespace Malumware.Common
{
    public static class BinaryReaderExtensions
    {
        public static byte[] ReadBytesUntil(this BinaryReader reader, byte terminator, int sizeHint = 1)
        {
            var bytes = new List<byte>(sizeHint); // Early hint for the size of the file name
            while (true)
            {
                var b = reader.ReadByte();
                if (b == terminator)
                {
                    break;
                }
                bytes.Add(b);
            }
            
            return bytes.ToArray();
        }
    }
}