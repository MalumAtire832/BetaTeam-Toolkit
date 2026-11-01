using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    // Writes a NiLight as a KHR_lights_punctual light on a child node. The child carries no extras: the light's own
    // node keeps every original field, and consumers count only nodes with extras as original nodes. Values keep the
    // FIN's own frame (Z-up, original units). The original attenuation stays in the extras, so no range is written.
    internal static class FinGltfLights
    {
        private const int DIRECTIONAL = 1;
        private const int POINT = 2;
        private const int SPOT = 3;
        private const float MIN_CONE = 1e-4f;
        private static readonly Vector3 GLTF_FORWARD = new Vector3(0, 0, -1);

        public static void Attach(ModelRoot model, Node node, NiLight light)
        {
            if (light.LightType is not (DIRECTIONAL or POINT or SPOT))
            {
                return;
            }

            var child = node.CreateNode("Light");
            child.LocalTransform = new AffineTransform(
                Vector3.One,
                light.LightType == POINT ? Quaternion.Identity : PointAlong(light.Direction),
                light.Location
            );
            var type = light.LightType switch
            {
                DIRECTIONAL => PunctualLightType.Directional,
                POINT => PunctualLightType.Point,
                _ => PunctualLightType.Spot
            };
            var punctual = model.CreatePunctualLight(type);
            punctual.Name = light.Name ?? "NiLight";
            var color = FinGltfColor.ToLinear(light.DiffuseColor.R, light.DiffuseColor.G, light.DiffuseColor.B, 1);
            punctual.SetColor(new Vector3(color.X, color.Y, color.Z), light.Dimmer);
            if (light.LightType == SPOT)
            {
                var outer = Math.Clamp(light.SpotAngle * MathF.PI / 180, MIN_CONE, MathF.PI / 2);
                punctual.SetSpotCone(0, outer);
            }
            child.PunctualLight = punctual;
        }

        private static Quaternion PointAlong(Vector3 direction)
        {
            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || !float.IsFinite(direction.Z)
                || direction.LengthSquared() == 0)
            {
                return Quaternion.Identity;
            }

            var target = Vector3.Normalize(direction);
            var dot = Vector3.Dot(GLTF_FORWARD, target);
            if (dot < -1 + 1e-6f)
            {
                return Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
            }

            var axis = Vector3.Cross(GLTF_FORWARD, target);

            return Quaternion.Normalize(new Quaternion(axis, 1 + dot));
        }
    }
}
