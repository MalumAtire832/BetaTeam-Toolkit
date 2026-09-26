using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    public class NiAVObject : NiObject
    {
        // Hidden by the game rather than by culling (engine: GetAppCulled)
        public bool AppCulled { get; private set; }

        public Vector3 Translation { get; private set; }
        public FinMatrix3 Rotation { get; private set; }
        public float Scale { get; private set; }
        // Engine: GetLocalVelocity
        public Vector3 Velocity { get; private set; }
        public IReadOnlyList<FinRef<NiProperty>> Properties { get; private set; } = [];

        // The engine's PropagateMode enum (GetCollisionPropagate); which number means what isn't confirmed yet
        public uint CollisionPropagate { get; private set; }

        // Collision shape, stored inline
        public NiBoundingVolume? BoundingVolume { get; private set; }

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            AppCulled = reader.ReadByte() != 0;
            Translation = reader.ReadVector3();
            Rotation = reader.ReadMatrix3();
            Scale = reader.ReadSingle();
            Velocity = reader.ReadVector3();
            Properties = reader.ReadRefList<NiProperty>();
            CollisionPropagate = reader.ReadUInt32();

            BoundingVolume = reader.ReadUInt32() != 0 ? NiBoundingVolume.Read(reader) : null;
        }
    }
}
