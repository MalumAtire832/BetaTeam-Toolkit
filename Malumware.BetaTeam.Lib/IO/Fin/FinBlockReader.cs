using System.Numerics;
using System.Text;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    public sealed class FinBlockReader
    {
        // Longest names and texture paths are well below this; a larger length means the stream is out of step
        public const int MAX_CSTRING_LENGTH = 1024;

        private const string BASE_EXTRA_DATA_CLASS = "NiExtraData";

        private readonly BinaryReader _reader;
        private readonly FinBlockRegistry _registry;
        private readonly List<FinRef> _refs = [];

        internal IReadOnlyList<FinRef> Refs => _refs;

        public long Position => _reader.BaseStream.Position;
        public long Length => _reader.BaseStream.Length;

        public FinBlockReader(BinaryReader reader, FinBlockRegistry registry)
        {
            _reader = reader;
            _registry = registry;
        }

        public byte ReadByte() => _reader.ReadByte();
        public ushort ReadUInt16() => _reader.ReadUInt16();
        public uint ReadUInt32() => _reader.ReadUInt32();
        public int ReadInt32() => _reader.ReadInt32();
        public float ReadSingle() => _reader.ReadSingle();

        public Vector2 ReadVector2()
        {
            return new Vector2(ReadSingle(), ReadSingle());
        }

        public Vector3 ReadVector3()
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        public FinColorA ReadColorA()
        {
            return new FinColorA(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public FinMatrix3 ReadMatrix3()
        {
            return new FinMatrix3(
                ReadSingle(), ReadSingle(), ReadSingle(),
                ReadSingle(), ReadSingle(), ReadSingle(),
                ReadSingle(), ReadSingle(), ReadSingle());
        }

        // u32 length + bytes, no terminator, no null case (class names and markers)
        public string ReadSizedString(int maxLength)
        {
            var offset = Position;
            var length = _reader.ReadUInt32();
            if (length > maxLength || length > Length - Position)
            {
                throw new InvalidDataException($"Invalid string length {length} at offset 0x{offset:X}");
            }
            return Encoding.ASCII.GetString(_reader.ReadBytes((int)length));
        }

        // u32 length + bytes; a length of zero (or negative) means null
        public string? ReadCString()
        {
            var offset = Position;
            var length = _reader.ReadInt32();
            if (length <= 0)
            {
                return null;
            }
            if (length > MAX_CSTRING_LENGTH || length > Length - Position)
            {
                throw new InvalidDataException($"Invalid string length {length} at offset 0x{offset:X}");
            }
            return Encoding.Latin1.GetString(_reader.ReadBytes(length));
        }

        // Guards against allocating huge arrays when the stream is out of step
        public int ReadCount(int elementSize)
        {
            var offset = Position;
            var count = _reader.ReadUInt32();
            if (count > (Length - Position) / elementSize)
            {
                throw new InvalidDataException(
                    $"Count {count} at offset 0x{offset:X} exceeds the {Length - Position} remaining bytes"
                );
            }
            return (int)count;
        }

        public T[] ReadArray<T>(int count, Func<FinBlockReader, T> read)
        {
            var values = new T[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = read(this);
            }
            return values;
        }

        public FinRef<T> ReadRef<T>() where T : NiObject
        {
            var reference = new FinRef<T>(ReadUInt32());
            _refs.Add(reference);
            return reference;
        }

        public IReadOnlyList<FinRef<T>> ReadRefList<T>() where T : NiObject
        {
            var count = ReadCount(sizeof(uint));
            return ReadArray(count, r => r.ReadRef<T>());
        }

        internal IReadOnlyList<NiExtraData> ReadExtraDataList()
        {
            var count = ReadCount(sizeof(uint));
            var list = new List<NiExtraData>(count);
            for (var i = 0; i < count; i++)
            {
                var offset = Position;
                // A missing class name makes the engine fall back to the base NiExtraData loader
                var className = ReadCString() ?? BASE_EXTRA_DATA_CLASS;
                var extraData = _registry.CreateExtraData(className)
                    ?? throw FinUnknownClass.Exception(className, offset);
                extraData.ClassName = className;
                extraData.Offset = offset;
                extraData.Load(this);
                list.Add(extraData);
            }
            return list;
        }
    }
}
