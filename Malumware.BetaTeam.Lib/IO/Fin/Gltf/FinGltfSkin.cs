using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    // The game draws a skinned mesh from its bones, not from its stored vertices: each vertex is its offset from a bone,
    // moved by that bone's transform. The stored vertices are in another frame and are never used. This bakes the rest
    // pose into the mesh and lists the bones as glTF joints, so posing the bones moves the mesh as it does in the game.
    internal sealed class FinGltfSkin
    {
        // glTF holds four joints per vertex in JOINTS_0; shipped skins have one bone per vertex
        public const int MAX_INFLUENCES = 4;

        public IReadOnlyList<Ni3dsBone> Bones { get; }
        public Vector3[] Vertices { get; }
        public Vector3[]? Normals { get; }
        public Vector4[] Joints { get; }
        public Vector4[] Weights { get; }

        private FinGltfSkin(IReadOnlyList<Ni3dsBone> bones, Vector3[] vertices, Vector3[]? normals, Vector4[] joints, Vector4[] weights)
        {
            Bones = bones;
            Vertices = vertices;
            Normals = normals;
            Joints = joints;
            Weights = weights;
        }

        public static FinGltfSkin Create(Ni3dsSkin skin, IReadOnlyDictionary<NiAVObject, NiNode> parents)
        {
            var influences = skin.SkinVertices ?? throw new ArgumentException("The skin has no skin data", nameof(skin));
            var bones = new List<Ni3dsBone>();
            var boneIndices = new Dictionary<Ni3dsBone, int>(ReferenceEqualityComparer.Instance);
            var stacks = new Dictionary<Ni3dsBone, Matrix4x4>(ReferenceEqualityComparer.Instance);

            var vertices = new Vector3[influences.Count];
            var normals = skin.Normals is null ? null : new Vector3[influences.Count];
            var joints = new Vector4[influences.Count];
            var weights = new Vector4[influences.Count];
            for (var i = 0; i < influences.Count; i++)
            {
                var vertex = influences[i];
                if (vertex.Length is 0 or > MAX_INFLUENCES)
                {
                    throw new InvalidDataException(
                        $"{FinToGltfConverter.Describe(skin)} has a vertex with {vertex.Length} bones; 1 to {MAX_INFLUENCES} are supported"
                    );
                }

                for (var j = 0; j < vertex.Length; j++)
                {
                    var bone = vertex[j].Bone.Target
                        ?? throw new InvalidDataException($"{FinToGltfConverter.Describe(skin)} has a vertex without a bone");
                    if (!stacks.TryGetValue(bone, out var stack))
                    {
                        stack = BoneStack(bone, parents);
                        stacks[bone] = stack;
                        boneIndices[bone] = bones.Count;
                        bones.Add(bone);
                    }

                    // With a single bone the engine moves the vertex by it in full and ignores the weight
                    var weight = vertex.Length == 1 ? 1 : vertex[j].Weight;
                    vertices[i] += weight * Vector3.Transform(vertex[j].Offset, stack);
                    if (normals is not null)
                    {
                        normals[i] += weight * Vector3.TransformNormal(skin.Normals![i], stack);
                    }
                    joints[i] = WithComponent(joints[i], j, boneIndices[bone]);
                    weights[i] = WithComponent(weights[i], j, weight);
                }
            }

            return new FinGltfSkin(bones, vertices, normals, joints, weights);
        }

        // A bone's transform relative to its root bone's parent: its local transform, then each parent bone's in turn.
        // The engine starts every root bone from the identity, so whatever lies above the root bone doesn't count.
        private static Matrix4x4 BoneStack(Ni3dsBone bone, IReadOnlyDictionary<NiAVObject, NiNode> parents)
        {
            var stack = FinGltfTransform.ToLocalTransform(bone).Matrix;
            NiAVObject current = bone;
            for (var depth = 0; parents.TryGetValue(current, out var parent) && parent is Ni3dsBone; depth++)
            {
                if (depth >= FinToGltfConverter.MAX_DEPTH)
                {
                    throw new InvalidDataException($"{FinToGltfConverter.Describe(bone)} has more than {FinToGltfConverter.MAX_DEPTH} parent bones");
                }
                stack *= FinGltfTransform.ToLocalTransform(parent).Matrix;
                current = parent;
            }

            return stack;
        }

        private static Vector4 WithComponent(Vector4 vector, int index, float value)
        {
            vector[index] = value;

            return vector;
        }
    }
}
