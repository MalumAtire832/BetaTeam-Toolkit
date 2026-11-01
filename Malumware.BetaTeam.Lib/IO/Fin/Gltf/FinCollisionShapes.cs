using System.Numerics;
using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks.BoundingVolumes;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    // One OMI_physics_shape shape and where it sits in the node's frame (original units, Z-up)
    internal sealed record FinCollisionShape(JsonObject Shape, Vector3 Translation, Quaternion Rotation);

    // Turns the bounding volumes Godot can hold exactly into collision shapes. Inverted volumes, half-spaces,
    // intersections and lozenges have no Godot shape, so they are counted and stay in the node's extras only.
    internal static class FinCollisionShapes
    {
        private const float ORTHONORMAL_TOLERANCE = 1e-3f;
        private static readonly int[] FINAL_SHAPE_MODES = [0, 4, 6];

        public static (IReadOnlyList<FinCollisionShape> Shapes, int Skipped) FromVolume(NiBoundingVolume volume)
        {
            var shapes = new List<FinCollisionShape>();
            var skipped = 0;
            Collect(volume, shapes, ref skipped);

            return (shapes, skipped);
        }

        public static bool IsFinalShapeMode(uint collisionPropagate)
        {
            return Array.IndexOf(FINAL_SHAPE_MODES, (int)Math.Min(collisionPropagate, int.MaxValue)) >= 0;
        }

        private static void Collect(NiBoundingVolume volume, List<FinCollisionShape> shapes, ref int skipped)
        {
            switch (volume)
            {
                case NiUnionBV union:
                    foreach (var child in union.Volumes)
                    {
                        Collect(child, shapes, ref skipped);
                    }

                    return;
                case NiBoxBV { Inverted: false } box:
                    Add(FromBox(box), shapes, ref skipped);

                    return;
                case NiSphereBV { Inverted: false } sphere:
                    Add(FromSphere(sphere.Center, sphere.Radius), shapes, ref skipped);

                    return;
                case NiCapsuleBV { Inverted: false } capsule:
                    Add(FromCapsule(capsule), shapes, ref skipped);

                    return;
                default:
                    skipped++;

                    return;
            }
        }

        private static void Add(FinCollisionShape? shape, List<FinCollisionShape> shapes, ref int skipped)
        {
            if (shape is null)
            {
                skipped++;
            }
            else
            {
                shapes.Add(shape);
            }
        }

        private static FinCollisionShape? FromBox(NiBoxBV box)
        {
            if (box.Axes.Length != 3 || !IsFinite(box.Center) || !IsFinite(box.Extents)
                || box.Extents.X <= 0 || box.Extents.Y <= 0 || box.Extents.Z <= 0)
            {
                return null;
            }

            var x = box.Axes[0];
            var y = box.Axes[1];
            var z = box.Axes[2];
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z) || !IsOrthonormal(x, y, z))
            {
                return null;
            }

            // A left-handed basis is the same box with one axis flipped, as boxes are symmetric
            if (Vector3.Dot(Vector3.Cross(x, y), z) < 0)
            {
                z = -z;
            }

            // Row-vector convention: the rows of the matrix are the images of the local axes
            var matrix = new Matrix4x4(
                x.X, x.Y, x.Z, 0,
                y.X, y.Y, y.Z, 0,
                z.X, z.Y, z.Z, 0,
                0, 0, 0, 1);
            var rotation = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(matrix));
            var size = box.Extents * 2;
            var shape = new JsonObject
            {
                ["type"] = "box",
                ["box"] = new JsonObject { ["size"] = new JsonArray(size.X, size.Y, size.Z) }
            };

            return new FinCollisionShape(shape, box.Center, rotation);
        }

        private static FinCollisionShape? FromSphere(Vector3 center, float radius)
        {
            if (!IsFinite(center) || !float.IsFinite(radius) || radius <= 0)
            {
                return null;
            }

            var shape = new JsonObject
            {
                ["type"] = "sphere",
                ["sphere"] = new JsonObject { ["radius"] = radius }
            };

            return new FinCollisionShape(shape, center, Quaternion.Identity);
        }

        private static FinCollisionShape? FromCapsule(NiCapsuleBV capsule)
        {
            if (!IsFinite(capsule.Origin) || !IsFinite(capsule.Direction) || !float.IsFinite(capsule.Radius)
                || capsule.Radius <= 0)
            {
                return null;
            }

            var length = capsule.Direction.Length();
            var center = capsule.Origin + capsule.Direction / 2;
            if (length == 0)
            {
                return FromSphere(center, capsule.Radius);
            }

            // The height of an OMI capsule includes both caps
            var shape = new JsonObject
            {
                ["type"] = "capsule",
                ["capsule"] = new JsonObject
                {
                    ["radius"] = capsule.Radius,
                    ["height"] = length + 2 * capsule.Radius
                }
            };

            return new FinCollisionShape(shape, center, FinGltfTransform.ShortestArc(Vector3.UnitY, capsule.Direction));
        }

        private static bool IsOrthonormal(Vector3 x, Vector3 y, Vector3 z)
        {
            return MathF.Abs(x.Length() - 1) <= ORTHONORMAL_TOLERANCE
                && MathF.Abs(y.Length() - 1) <= ORTHONORMAL_TOLERANCE
                && MathF.Abs(z.Length() - 1) <= ORTHONORMAL_TOLERANCE
                && MathF.Abs(Vector3.Dot(x, y)) <= ORTHONORMAL_TOLERANCE
                && MathF.Abs(Vector3.Dot(x, z)) <= ORTHONORMAL_TOLERANCE
                && MathF.Abs(Vector3.Dot(y, z)) <= ORTHONORMAL_TOLERANCE;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }
}
