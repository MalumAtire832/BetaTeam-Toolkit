using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Animation;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // What every instance of an object type shares: description, shadow settings, skills (named animation clips),
    // keyframe tracks and floor points. The object name is the type name (such as the unit ID).
    public class DDActorSharedData : NiObject
    {
        public string? Description { get; private set; }
        // The behaviour DLL (in Bhvr.pac) that holds the object's code, without ".dll". When absent, the engine's
        // LoadActorDLL uses the type name, so objects without code of their own can share another object's DLL.
        public string? BehaviorName { get; private set; }
        public bool MakeShadow { get; private set; }
        public bool IsProp { get; private set; }
        public float ShadowMultiplier { get; private set; }
        public uint Unknown74 { get; private set; }
        public IReadOnlyList<FinSkill> Skills { get; private set; } = [];
        public IReadOnlyList<FinAnimationTrack> Tracks { get; private set; } = [];
        public Vector3[] FloorPoints { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Description = reader.ReadCString();
            BehaviorName = reader.ReadCString();
            MakeShadow = reader.ReadByte() != 0;
            IsProp = reader.ReadByte() != 0;
            ShadowMultiplier = reader.ReadSingle();
            Unknown74 = reader.ReadUInt32();

            var skillCount = reader.ReadSignedCount(4);
            Skills = reader.ReadArray(skillCount, FinSkill.Read);

            var trackCount = reader.ReadSignedCount(4);
            Tracks = reader.ReadArray(trackCount, FinAnimationTrack.Read);

            var pointCount = reader.ReadSignedCount(12);
            FloorPoints = reader.ReadArray(pointCount, r => r.ReadVector3());
        }
    }
}
