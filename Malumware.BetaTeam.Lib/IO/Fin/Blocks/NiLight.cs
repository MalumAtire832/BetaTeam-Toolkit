using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // One class for every kind of light; LightType tells them apart
    public class NiLight : NiAVObject
    {
        public Vector3 Location { get; private set; }
        public Vector3 Direction { get; private set; }
        public bool LightSwitch { get; private set; }
        public float SpotAngle { get; private set; }
        public float SpotExponent { get; private set; }
        public float Dimmer { get; private set; }
        public FinColor3 AmbientColor { get; private set; }
        public FinColor3 DiffuseColor { get; private set; }
        public FinColor3 SpecularColor { get; private set; }
        public float AttenuationDistance { get; private set; }
        public float AttenuationCurve { get; private set; }
        public bool Attenuation { get; private set; }
        public int LightType { get; private set; }

        // Link IDs of the nodes this light shines on. The engine reads and discards them: nodes list their lights
        // instead, so these are kept as plain numbers rather than links.
        public uint[] IlluminatedNodes { get; private set; } = [];

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            Location = reader.ReadVector3();
            Direction = reader.ReadVector3();
            LightSwitch = reader.ReadByte() != 0;
            SpotAngle = reader.ReadSingle();
            SpotExponent = reader.ReadSingle();
            Dimmer = reader.ReadSingle();
            AmbientColor = reader.ReadColor3();
            DiffuseColor = reader.ReadColor3();
            SpecularColor = reader.ReadColor3();
            AttenuationDistance = reader.ReadSingle();
            AttenuationCurve = reader.ReadSingle();
            Attenuation = reader.ReadByte() != 0;
            LightType = reader.ReadInt32();
            var count = reader.ReadCount(sizeof(uint));
            IlluminatedNodes = reader.ReadArray(count, r => r.ReadUInt32());
        }
    }
}
