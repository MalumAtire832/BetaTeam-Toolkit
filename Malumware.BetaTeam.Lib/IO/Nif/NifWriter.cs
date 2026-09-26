using System.Numerics;
using System.Text;
using Malumware.BetaTeam.Lib.IO.Nif.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Nif
{
    // Writes NetImmerse 4.0.0.2, the version Morrowind uses. FIN's blocks don't match the 3.x layouts, and nif.xml
    // (which NifSkope reads files with) marks 3.1 and older as not fully supported.
    public sealed class NifWriter
    {
        public const uint VERSION = 0x04000002;
        public const string HEADER_STRING = "NetImmerse File Format, Version 4.0.0.2";

        private readonly BinaryWriter _writer;
        private readonly Dictionary<NiObject, int> _indices;

        private NifWriter(BinaryWriter writer, Dictionary<NiObject, int> indices)
        {
            _writer = writer;
            _indices = indices;
        }

        public static void Write(NifFile file, Stream stream)
        {
            var blocks = file.CollectBlocks();
            var indices = new Dictionary<NiObject, int>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < blocks.Count; i++)
            {
                indices[blocks[i]] = i;
            }

            using var binaryWriter = new BinaryWriter(stream, Encoding.Latin1, leaveOpen: true);
            var writer = new NifWriter(binaryWriter, indices);

            binaryWriter.Write(Encoding.ASCII.GetBytes(HEADER_STRING + "\n"));
            binaryWriter.Write(VERSION);
            binaryWriter.Write((uint)blocks.Count);

            // Before version 5 there is no block type table: each block starts with its class name
            foreach (var block in blocks)
            {
                writer.WriteSizedString(block.ClassName);
                block.Write(writer);
            }

            binaryWriter.Write((uint)file.Roots.Count);
            foreach (var root in file.Roots)
            {
                writer.WriteRef(root);
            }
        }

        public static byte[] Write(NifFile file)
        {
            using var stream = new MemoryStream();
            Write(file, stream);
            return stream.ToArray();
        }

        public void WriteByte(byte value) => _writer.Write(value);
        public void WriteUInt16(ushort value) => _writer.Write(value);
        public void WriteInt16(short value) => _writer.Write(value);
        public void WriteUInt32(uint value) => _writer.Write(value);
        public void WriteSingle(float value) => _writer.Write(value);

        // Booleans take four bytes up to and including 4.0.0.2, one byte from 4.1.0.1 on
        public void WriteBool(bool value) => _writer.Write(value ? 1u : 0u);

        public void WriteCount(int count) => _writer.Write((uint)count);

        public void WriteSizedString(string value)
        {
            var bytes = Encoding.Latin1.GetBytes(value);
            _writer.Write((uint)bytes.Length);
            _writer.Write(bytes);
        }

        public void WriteVector2(Vector2 value)
        {
            _writer.Write(value.X);
            _writer.Write(value.Y);
        }

        public void WriteVector3(Vector3 value)
        {
            _writer.Write(value.X);
            _writer.Write(value.Y);
            _writer.Write(value.Z);
        }

        public void WriteMatrix33(NifMatrix33 value)
        {
            _writer.Write(value.M11);
            _writer.Write(value.M12);
            _writer.Write(value.M13);
            _writer.Write(value.M21);
            _writer.Write(value.M22);
            _writer.Write(value.M23);
            _writer.Write(value.M31);
            _writer.Write(value.M32);
            _writer.Write(value.M33);
        }

        public void WriteColor3(NifColor3 value)
        {
            _writer.Write(value.R);
            _writer.Write(value.G);
            _writer.Write(value.B);
        }

        public void WriteColor4(NifColor4 value)
        {
            _writer.Write(value.R);
            _writer.Write(value.G);
            _writer.Write(value.B);
            _writer.Write(value.A);
        }

        // A block's index in the file, or -1 for none
        public void WriteRef(NiObject? block)
        {
            if (block is null)
            {
                _writer.Write(-1);
                return;
            }

            if (!_indices.TryGetValue(block, out var index))
            {
                throw new InvalidOperationException($"{block.ClassName} is linked but not part of the file");
            }
            _writer.Write(index);
        }

        public void WriteRefList(IReadOnlyCollection<NiObject?> blocks)
        {
            WriteCount(blocks.Count);
            foreach (var block in blocks)
            {
                WriteRef(block);
            }
        }
    }
}
