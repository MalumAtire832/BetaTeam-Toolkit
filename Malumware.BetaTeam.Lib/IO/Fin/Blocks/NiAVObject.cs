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

        // The engine's PropagateMode enum (GetCollisionPropagate): how collision tests treat this object. 0, 4 and 6
        // collide with its bounding volume as the final shape; 1, 2, 5 and 7 descend, so geometry below collides with
        // its triangles; 3 doesn't collide with anything in the subtree. 4 and 5 don't collide while the object is
        // hidden (AppCulled); hidden objects in the other modes still collide.
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
