using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiAVObject : NiObject
    {
        // Hidden by the game rather than by culling
        public bool AppCulled { get; private set; }

        public Vector3 Translation { get; private set; }
        public FinMatrix3 Rotation { get; private set; }
        public float Scale { get; private set; }
        public Vector3 Velocity { get; private set; }
        public IReadOnlyList<FinRef<NiObject>> Properties { get; private set; } = [];

        // The engine's PropagateMode enum; which number means what isn't confirmed yet
        public uint CollisionPropagate { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            AppCulled = reader.ReadByte() != 0;
            Translation = reader.ReadVector3();
            Rotation = reader.ReadMatrix3();
            Scale = reader.ReadSingle();
            Velocity = reader.ReadVector3();
            Properties = reader.ReadRefList<NiObject>();
            CollisionPropagate = reader.ReadUInt32();

            if (reader.ReadUInt32() != 0)
            {
                throw new InvalidDataException(
                    $"{ClassName} at offset 0x{Offset:X} has a bounding volume, which isn't supported"
                );
            }
        }
    }
}
