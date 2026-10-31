using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Animation
{
    // Builds small animated FIN files in code: an actor with its shared data (skills and tracks), animated nodes,
    // shapes, flip-books, material animators and morph shapes. Every key written is linear.
    internal static class AnimatedFin
    {
        private const uint PRESENT = 0x0A96AC58;    // Optional arrays are preceded by a non-zero address when present
        private const uint LINEAR = 1;
        private const uint BARY_MORPH = 5;

        public sealed record Skill(string Name, float Start, float End, ushort[]? Nodes = null, ushort[]? Actions = null,
            ushort[]? Others = null);

        public sealed record Track(
            (float Time, Vector3 Value)[]? Positions = null,
            (float Time, Quaternion Value)[]? Rotations = null,
            (float Time, float Value)[]? Scales = null,
            (float Time, bool Visible)[]? Visibility = null);

        public static FinStreamBuilder Start()
        {
            return new FinStreamBuilder().Header();
        }

        public static FinFile Read(FinStreamBuilder builder)
        {
            return FinReader.Read("TEST", builder.EndOfFile().ToArray());
        }

        public static FinStreamBuilder Actor(this FinStreamBuilder builder, uint linkId, uint sharedData, params uint[] children)
        {
            return builder.TopLevel().SizedString("DDUnit").NiObject(linkId, "Actor")
                .NiAVObjectFields()
                .NiNodeFields(children, [])
                .UInt32(sharedData);
        }

        public static FinStreamBuilder Node(this FinStreamBuilder builder, uint linkId, string name, params uint[] children)
        {
            return builder.SizedString("NiNode").NiObject(linkId, name).NiAVObjectFields().NiNodeFields(children, []);
        }

        // An animated node with its own (empty) keys; the actor's shared data supplies the keys the game uses. The
        // rest rotation is the identity unless given, as 9 floats in file order.
        public static FinStreamBuilder AnimNode(this FinStreamBuilder builder, uint linkId, string name, uint[] children,
            float[]? rotation = null, float scale = 1, uint[]? properties = null)
        {
            builder = builder.SizedString("Ni3dsAnimationNode").NiObject(linkId, name)
                .Byte(0)
                .Floats(0, 0, 0)
                .Floats(rotation ?? [1, 0, 0, 0, 1, 0, 0, 0, 1])
                .Floats(scale)
                .Floats(0, 0, 0)
                .Refs(properties ?? [])
                .UInt32(0)
                .UInt32(0)
                .NiNodeFields(children, []);

            return Core(builder)
                .Int32(0).Int32(0).Int32(0)         // rotation, position and scale keys
                .Int32(0);                          // visibility keys
        }

        // Clamp to a range wider than any test uses, frequency 1, phase 0
        public static FinStreamBuilder Core(this FinStreamBuilder builder)
        {
            return builder.UInt32(0).Byte(0).Byte(0).Byte(0).Byte(1).UInt32(2).Floats(0, 1, 0, 0, 1_000_000);
        }

        // A lit triangle with one texture set
        public static FinStreamBuilder Shape(this FinStreamBuilder builder, uint linkId, string name, params uint[] properties)
        {
            return ShapeGeometry(builder.SizedString("NiTriShape").NiObject(linkId, name), properties, 1)
                .UInt16(0).UInt16(1).UInt16(2);
        }

        // A triangle without normals, which the glb makes unlit
        public static FinStreamBuilder UnlitShape(this FinStreamBuilder builder, uint linkId, string name,
            params uint[] properties)
        {
            return ShapeGeometry(builder.SizedString("NiTriShape").NiObject(linkId, name), properties, 1, normals: false)
                .UInt16(0).UInt16(1).UInt16(2);
        }

        public static FinStreamBuilder SharedData(this FinStreamBuilder builder, uint linkId, Skill[] skills, Track[] tracks)
        {
            builder = builder.SizedString("DDActorSharedData").NiObject(linkId, "U0001")
                .CString("Test unit").CString(null).Byte(0).Byte(0).Floats(1, 0)
                .Int32(skills.Length);
            foreach (var skill in skills)
            {
                builder = builder.CString(skill.Name).Floats(skill.Start, skill.End).Byte(0);
                builder = Indices(builder, skill.Nodes);
                builder = Indices(builder, skill.Actions);
                builder = Indices(builder, skill.Others);
            }

            builder = builder.Int32(tracks.Length);
            foreach (var track in tracks)
            {
                builder = Rotations(builder, track.Rotations ?? []);
                builder = Positions(builder, track.Positions ?? []);
                builder = Scales(builder, track.Scales ?? []);
                builder = builder.Int32(track.Visibility?.Length ?? 0);
                foreach (var (time, visible) in track.Visibility ?? [])
                {
                    builder = builder.Floats(time).Byte(visible ? (byte)1 : (byte)0);
                }
            }

            return builder.UInt32(0);               // floor points
        }

        public static FinStreamBuilder Image(this FinStreamBuilder builder, uint linkId, string fileName)
        {
            return builder.SizedString("NiImage").NiObject(linkId).Byte(1).CString(fileName).UInt32(0);
        }

        // A texture property tied to a flip-book by Digital Domain's extra data, and the flip-book itself
        public static FinStreamBuilder FlipBook(this FinStreamBuilder builder, uint textureId, uint flipId, int index,
            uint outOfBound, float startTime, float cycleTime, params uint[] images)
        {
            return builder.SizedString("NiTextureProperty")
                .UInt32(textureId).CString(null)
                .UInt32(1).CString("TexturePropExtraData").UInt32(8).UInt32(flipId).Int32(index)
                .Byte(1).Int32(0).Refs(images)
                .SizedString("NiFlipTextures").NiObject(flipId).UInt32(outOfBound).Floats(1, startTime, cycleTime)
                .UInt32(textureId);
        }

        public static FinStreamBuilder Material(this FinStreamBuilder builder, uint linkId, Vector3 diffuse, float alpha,
            params uint[] animators)
        {
            builder = builder.SizedString("NiMaterialProperty").UInt32(linkId).CString(null).UInt32((uint)animators.Length);
            foreach (var animator in animators)
            {
                builder = builder.CString("Ni3dsPropAnimExtraData").UInt32(4).UInt32(animator);
            }

            return builder.Byte(1)
                .Floats(0.2f, 0.2f, 0.2f)
                .Floats(diffuse.X, diffuse.Y, diffuse.Z)
                .Floats(0, 0, 0)
                .Floats(0, 0, 0)
                .Floats(10, alpha);
        }

        // field: 0 ambient, 1 diffuse, 2 specular, 3 emissive
        public static FinStreamBuilder ColorAnimator(this FinStreamBuilder builder, uint linkId, uint target, uint field,
            params (float Time, Vector3 Value)[] keys)
        {
            builder = Core(builder.SizedString("Ni3dsColorAnimator").NiObject(linkId))
                .UInt32(target).UInt32(field).Int32(keys.Length).UInt32(LINEAR);
            foreach (var (time, value) in keys)
            {
                builder = builder.Floats(time, value.X, value.Y, value.Z);
            }

            return builder;
        }

        public static FinStreamBuilder AlphaAnimator(this FinStreamBuilder builder, uint linkId, uint target,
            params (float Time, float Value)[] keys)
        {
            builder = Core(builder.SizedString("Ni3dsAlphaAnimator").NiObject(linkId))
                .UInt32(target).Int32(keys.Length).UInt32(LINEAR);
            foreach (var (time, value) in keys)
            {
                builder = builder.Floats(time, value);
            }

            return builder;
        }

        // A morphing triangle: positions of every target, and barycentric keys with a weight per target and zero
        // tangents
        public static FinStreamBuilder MorphShape(this FinStreamBuilder builder, uint linkId, string name,
            Vector3[][] targets, params (float Time, float[] Weights)[] keys)
        {
            builder = ShapeGeometry(builder.SizedString("Ni3dsMorphShape").NiObject(linkId, name), [], 1, targets[0])
                .UInt16(0).UInt16(1).UInt16(2);
            builder = Core(builder)
                .Byte(0).Byte(0)
                .Int32(targets.Length).Int32(keys.Length)
                .Floats(0, 0, 0, 1)
                .UInt32(BARY_MORPH);
            foreach (var (time, weights) in keys)
            {
                builder = builder.Floats(time, 0, 0, 0, 0, 0, 0).Int32(weights.Length).Floats(weights)
                    .Floats(new float[weights.Length])
                    .Floats(new float[weights.Length]);
            }
            foreach (var target in targets)
            {
                foreach (var vertex in target)
                {
                    builder = builder.Floats(vertex.X, vertex.Y, vertex.Z);
                }
            }

            return builder;
        }

        private static FinStreamBuilder ShapeGeometry(FinStreamBuilder builder, uint[] properties, int textureSets,
            Vector3[]? vertices = null, bool normals = true)
        {
            vertices ??= [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
            builder = builder.NiAVObjectFields(properties)
                .UInt16(3)
                .UInt32(PRESENT);
            foreach (var vertex in vertices)
            {
                builder = builder.Floats(vertex.X, vertex.Y, vertex.Z);
            }

            builder = normals
                ? builder.UInt32(PRESENT).Floats(0, 0, 1, 0, 0, 1, 0, 0, 1)
                : builder.UInt32(0);
            builder = builder
                .Floats(0.5f, 0.5f, 0, 0.75f)
                .UInt16(1)
                .UInt16((ushort)textureSets);
            if (textureSets > 0)
            {
                builder = builder.UInt32(PRESENT);
                for (var set = 0; set < textureSets; set++)
                {
                    builder = builder.Floats(0, 0, 0, 1, 0, 0, 0, 1, 0);
                }
            }
            else
            {
                builder = builder.UInt32(0);
            }

            return builder.UInt32(0).UInt32(0);     // no colours, no triangle planes
        }

        private static FinStreamBuilder Indices(FinStreamBuilder builder, ushort[]? indices)
        {
            builder = builder.Int32(indices?.Length ?? 0);
            foreach (var index in indices ?? [])
            {
                builder = builder.UInt16(index);
            }

            return builder;
        }

        private static FinStreamBuilder Rotations(FinStreamBuilder builder, (float Time, Quaternion Value)[] keys)
        {
            builder = builder.Int32(keys.Length);
            if (keys.Length > 0)
            {
                builder = builder.UInt32(LINEAR);
            }
            foreach (var (time, q) in keys)
            {
                builder = builder.Floats(time, 0, 1, 0, 0, q.W, q.X, q.Y, q.Z).Int32(0).UInt32(0);
            }

            return builder;
        }

        private static FinStreamBuilder Positions(FinStreamBuilder builder, (float Time, Vector3 Value)[] keys)
        {
            builder = builder.Int32(keys.Length);
            if (keys.Length > 0)
            {
                builder = builder.UInt32(LINEAR);
            }
            foreach (var (time, value) in keys)
            {
                builder = builder.Floats(time, value.X, value.Y, value.Z);
            }

            return builder;
        }

        private static FinStreamBuilder Scales(FinStreamBuilder builder, (float Time, float Value)[] keys)
        {
            builder = builder.Int32(keys.Length);
            if (keys.Length > 0)
            {
                builder = builder.UInt32(LINEAR);
            }
            foreach (var (time, value) in keys)
            {
                builder = builder.Floats(time, value);
            }

            return builder;
        }
    }
}
