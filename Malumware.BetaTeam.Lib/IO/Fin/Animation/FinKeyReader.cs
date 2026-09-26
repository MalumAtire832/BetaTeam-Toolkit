namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // Each key kind has a table of loaders indexed by the key type; the numbers match the engine's tables
    internal static class FinKeyReader
    {
        private const uint LINEAR = 1;
        private const uint BEZIER = 2;
        private const uint TCB = 3;
        private const uint EULER = 4;
        private const uint MORPH = 4;
        private const uint BARY_MORPH = 5;
        private const uint CUBIC_MORPH = 6;

        // The smallest key of each kind, used to reject counts that can't fit in the file. Float keys of the abstract
        // morph type take no bytes at all, so their count is only held to the file size.
        private const int MIN_FLOAT_KEY_SIZE = 1;
        private const int MIN_POS_KEY_SIZE = 16;
        private const int MIN_ROT_KEY_SIZE = 44;

        // Count, then the key type only when there are keys
        public static FinKeyGroup<FinFloatKey> ReadOptionalFloatKeys(FinBlockReader reader)
        {
            var count = reader.ReadSignedCount(MIN_FLOAT_KEY_SIZE);
            return count == 0 ? FinKeyGroup<FinFloatKey>.Empty : ReadFloatKeys(reader, count, reader.ReadUInt32());
        }

        public static FinKeyGroup<FinPosKey> ReadOptionalPosKeys(FinBlockReader reader)
        {
            var count = reader.ReadSignedCount(MIN_POS_KEY_SIZE);
            return count == 0 ? FinKeyGroup<FinPosKey>.Empty : ReadPosKeys(reader, count, reader.ReadUInt32());
        }

        public static FinKeyGroup<FinRotKey> ReadOptionalRotKeys(FinBlockReader reader)
        {
            var count = reader.ReadSignedCount(MIN_ROT_KEY_SIZE);
            return count == 0 ? FinKeyGroup<FinRotKey>.Empty : ReadRotKeys(reader, count, reader.ReadUInt32());
        }

        public static FinVisKey[] ReadVisKeys(FinBlockReader reader)
        {
            var count = reader.ReadSignedCount(5);
            return reader.ReadArray(count, r => new FinVisKey(r.ReadSingle(), r.ReadByte() != 0));
        }

        // Count and key type, the type present even when there are no keys
        public static FinKeyGroup<FinFloatKey> ReadFloatKeys(FinBlockReader reader)
        {
            var count = reader.ReadSignedCount(MIN_FLOAT_KEY_SIZE);
            return ReadFloatKeys(reader, count, reader.ReadUInt32());
        }

        public static FinKeyGroup<FinPosKey> ReadPosKeys(FinBlockReader reader)
        {
            var count = reader.ReadSignedCount(MIN_POS_KEY_SIZE);
            return ReadPosKeys(reader, count, reader.ReadUInt32());
        }

        public static FinKeyGroup<FinFloatKey> ReadFloatKeys(FinBlockReader reader, int count, uint type)
        {
            var offset = reader.Position;
            var keys = reader.ReadArray(count, r => ReadFloatKey(r, type, offset));
            return new FinKeyGroup<FinFloatKey>(type, keys);
        }

        private static FinKeyGroup<FinPosKey> ReadPosKeys(FinBlockReader reader, int count, uint type)
        {
            var offset = reader.Position;
            var keys = reader.ReadArray<FinPosKey?>(count, r => ReadPosKey(r, type, offset));
            return new FinKeyGroup<FinPosKey>(type, keys);
        }

        private static FinKeyGroup<FinRotKey> ReadRotKeys(FinBlockReader reader, int count, uint type)
        {
            var offset = reader.Position;
            var keys = reader.ReadArray<FinRotKey?>(count, r => ReadRotKey(r, type, offset));
            return new FinKeyGroup<FinRotKey>(type, keys);
        }

        private static FinFloatKey? ReadFloatKey(FinBlockReader reader, uint type, long offset)
        {
            if (type == MORPH)
            {
                // The engine registers a loader for the abstract morph key that creates nothing and reads nothing
                return null;
            }

            if (type is not (LINEAR or BEZIER or TCB or BARY_MORPH or CUBIC_MORPH))
            {
                throw UnknownType("float", type, offset);
            }

            var time = reader.ReadSingle();
            var value = reader.ReadSingle();
            if (type == LINEAR)
            {
                return new FinLinearFloatKey(time, value);
            }
            if (type == BEZIER)
            {
                return new FinBezierFloatKey(time, value, reader.ReadSingle(), reader.ReadSingle());
            }

            var tension = reader.ReadSingle();
            var continuity = reader.ReadSingle();
            var bias = reader.ReadSingle();
            var ds = reader.ReadSingle();
            var dd = reader.ReadSingle();
            if (type == TCB)
            {
                return new FinTcbFloatKey(time, value, tension, continuity, bias, ds, dd);
            }
            if (type == CUBIC_MORPH)
            {
                return new FinCubicMorphKey(time, value, tension, continuity, bias, ds, dd, reader.ReadSingle(), reader.ReadSingle());
            }

            var count = reader.ReadCount(3 * sizeof(uint));
            return new FinBaryMorphKey(
                time, value, tension, continuity, bias, ds, dd,
                reader.ReadArray(count, r => r.ReadUInt32()),
                reader.ReadArray(count, r => r.ReadUInt32()),
                reader.ReadArray(count, r => r.ReadUInt32()));
        }

        private static FinPosKey ReadPosKey(FinBlockReader reader, uint type, long offset)
        {
            if (type is not (LINEAR or BEZIER or TCB))
            {
                throw UnknownType("position", type, offset);
            }

            var time = reader.ReadSingle();
            var value = reader.ReadVector3();
            if (type == LINEAR)
            {
                return new FinLinearPosKey(time, value);
            }
            if (type == BEZIER)
            {
                return new FinBezierPosKey(time, value, reader.ReadVector3(), reader.ReadVector3(), reader.ReadVector3(), reader.ReadVector3());
            }
            return new FinTcbPosKey(
                time, value, reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadVector3(), reader.ReadVector3(), reader.ReadVector3(), reader.ReadVector3());
        }

        private static FinRotKey ReadRotKey(FinBlockReader reader, uint type, long offset)
        {
            if (type is not (LINEAR or BEZIER or TCB or EULER))
            {
                throw UnknownType("rotation", type, offset);
            }

            var time = reader.ReadSingle();
            var angle = reader.ReadSingle();
            var axis = reader.ReadVector3();
            var quaternion = ReadQuaternion(reader);
            var extraSpins = reader.ReadInt32();
            var unknown2C = reader.ReadUInt32();
            if (type == LINEAR)
            {
                return new FinLinearRotKey(time, angle, axis, quaternion, extraSpins, unknown2C);
            }
            if (type == BEZIER)
            {
                return new FinBezierRotKey(time, angle, axis, quaternion, extraSpins, unknown2C, ReadQuaternion(reader), reader.ReadSingle());
            }
            if (type == TCB)
            {
                return new FinTcbRotKey(
                    time, angle, axis, quaternion, extraSpins, unknown2C,
                    reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                    ReadQuaternion(reader), ReadQuaternion(reader), reader.ReadSingle(), reader.ReadSingle());
            }
            return new FinEulerRotKey(
                time, angle, axis, quaternion, extraSpins, unknown2C,
                reader.ReadUInt16(), ReadOptionalFloatKeys(reader), ReadOptionalFloatKeys(reader), ReadOptionalFloatKeys(reader));
        }

        private static FinQuaternion ReadQuaternion(FinBlockReader reader)
        {
            return new FinQuaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static InvalidDataException UnknownType(string kind, uint type, long offset)
        {
            return new InvalidDataException($"Unknown {kind} key type {type} at offset 0x{offset:X}");
        }
    }
}
