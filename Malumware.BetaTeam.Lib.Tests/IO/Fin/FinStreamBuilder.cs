using System.Text;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin
{
    internal sealed class FinStreamBuilder
    {
        private readonly MemoryStream _stream = new();
        private readonly BinaryWriter _writer;

        public FinStreamBuilder()
        {
            _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
        }

        public FinStreamBuilder Header(int version = 23)
        {
            _writer.Write(Encoding.ASCII.GetBytes($"Dweezil {version}\n"));
            return this;
        }

        // BinaryWriter.Write(string) uses a 7-bit length prefix, FIN files use a u32
        public FinStreamBuilder SizedString(string value)
        {
            _writer.Write(value.Length);
            _writer.Write(Encoding.ASCII.GetBytes(value));
            return this;
        }

        public FinStreamBuilder CString(string? value)
        {
            if (value is null)
            {
                _writer.Write(0);
                return this;
            }
            return SizedString(value);
        }

        public FinStreamBuilder Byte(byte value)
        {
            _writer.Write(value);
            return this;
        }

        public FinStreamBuilder UInt16(ushort value)
        {
            _writer.Write(value);
            return this;
        }

        public FinStreamBuilder UInt32(uint value)
        {
            _writer.Write(value);
            return this;
        }

        public FinStreamBuilder Int32(int value)
        {
            _writer.Write(value);
            return this;
        }


        public FinStreamBuilder Floats(params float[] values)
        {
            foreach (var value in values)
            {
                _writer.Write(value);
            }
            return this;
        }

        public FinStreamBuilder Refs(params uint[] linkIds)
        {
            _writer.Write((uint)linkIds.Length);
            foreach (var linkId in linkIds)
            {
                _writer.Write(linkId);
            }
            return this;
        }

        public FinStreamBuilder TopLevel() => SizedString("Top Level Object");
        public FinStreamBuilder EndOfFile() => SizedString("End Of File");

        // The NiObject part every block starts with, without extra data
        public FinStreamBuilder NiObject(uint linkId, string? name = null)
        {
            return UInt32(linkId).CString(name).UInt32(0);
        }

        public byte[] ToArray()
        {
            _writer.Flush();
            return _stream.ToArray();
        }
    }
}
