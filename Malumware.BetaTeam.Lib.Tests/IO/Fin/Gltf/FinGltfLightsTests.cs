using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    public class FinGltfLightsTests
    {
        private const float TOLERANCE = 1e-5f;

        private static byte[] Light(
            int type, float[]? location = null, float[]? direction = null, float spotAngle = 0.5f, float dimmer = 0.75f)
        {
            return new FinStreamBuilder().Header().TopLevel()
                .SizedString("NiLight").NiObject(0x50, "Omni01").NiAVObjectFields()
                .Floats(location ?? [1, 2, 3])
                .Floats(direction ?? [0, 0, -1])
                .Byte(1)
                .Floats(spotAngle, 2, dimmer)
                .Floats(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f)
                .Floats(100, 1.5f).Byte(1).Int32(type)
                .Int32(0)
                .EndOfFile()
                .ToArray();
        }

        private static (byte[] Glb, ModelRoot Model) Convert(byte[] bytes)
        {
            var glb = new FinToGltfConverter().ToGlb(FinReader.Read("TEST", bytes));
            var model = ModelRoot.ParseGLB(glb, new ReadSettings { Validation = ValidationMode.Strict });

            return (glb, model);
        }

        private static Node LightNode(ModelRoot model)
        {
            var node = Assert.Single(model.LogicalNodes, e => e.Name == "Light");
            Assert.Equal("Omni01", node.VisualParent.Name);

            return node;
        }

        private static Vector3 Forward(ModelRoot model)
        {
            return Vector3.Transform(new Vector3(0, 0, -1), LightNode(model).LocalTransform.Rotation);
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.True((expected - actual).Length() < TOLERANCE, $"{expected} != {actual}");
        }

        [Fact]
        public void Attach_WritesPointLight_WhenLightTypeIsTwo()
        {
            // Arrange
            var bytes = Light(2);

            // Act
            var (glb, model) = Convert(bytes);

            // Assert
            var light = GlbTestReader.Json(glb)["extensions"]!["KHR_lights_punctual"]!["lights"]![0]!;
            Assert.Equal("point", light["type"]!.GetValue<string>());
            Assert.Equal(0.75f, light["intensity"]!.GetValue<float>());
            Assert.Null(light["range"]);
            var color = light["color"]!.AsArray().Select(e => e!.GetValue<float>()).ToArray();
            var expected = FinGltfColor.ToLinear(0.4f, 0.5f, 0.6f, 1);
            Assert.Equal([expected.X, expected.Y, expected.Z], color);
            var node = LightNode(model);
            Assert.Equal("Light", node.Name);
            Assert.Null(node.Extras);
            Assert.NotNull(node.PunctualLight);
        }

        [Theory]
        [InlineData(1f, 0f, 0f)]
        [InlineData(0f, 0f, -1f)]
        [InlineData(0f, 0f, 1f)]
        public void Attach_PointsDirectionalLightAlongDirection_WhenLightTypeIsOne(float x, float y, float z)
        {
            // Arrange
            var bytes = Light(1, direction: [x, y, z]);

            // Act
            var (_, model) = Convert(bytes);

            // Assert
            AssertNear(new Vector3(x, y, z), Forward(model));
        }

        [Fact]
        public void Attach_KeepsIdentity_WhenDirectionPointsDownMinusZ()
        {
            // Arrange
            var bytes = Light(1, location: [0, 0, 0], direction: [0, 0, -1]);

            // Act
            var (_, model) = Convert(bytes);

            // Assert
            Assert.Equal(Quaternion.Identity, LightNode(model).LocalTransform.Rotation);
        }

        [Fact]
        public void Attach_TurnsHalfTurnAboutX_WhenDirectionPointsUpPlusZ()
        {
            // Arrange
            var bytes = Light(1, location: [0, 0, 0], direction: [0, 0, 1]);

            // Act
            var (_, model) = Convert(bytes);

            // Assert
            var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI);
            var rotation = LightNode(model).LocalTransform.Rotation;
            Assert.True(MathF.Abs(Quaternion.Dot(expected, rotation)) > 1 - 1e-5f);
        }

        [Fact]
        public void Attach_WritesSpotCone_WhenLightTypeIsThree()
        {
            // Arrange
            var bytes = Light(3, spotAngle: 30);

            // Act
            var (glb, _) = Convert(bytes);

            // Assert
            var light = GlbTestReader.Json(glb)["extensions"]!["KHR_lights_punctual"]!["lights"]![0]!;
            Assert.Equal("spot", light["type"]!.GetValue<string>());
            Assert.Equal(MathF.PI / 6, light["spot"]!["outerConeAngle"]!.GetValue<float>(), 1e-5f);
            Assert.Equal(0f, light["spot"]!["innerConeAngle"]?.GetValue<float>() ?? 0f);
        }

        [Fact]
        public void Attach_WritesNoLight_WhenLightTypeIsAmbient()
        {
            // Arrange
            var bytes = Light(0);

            // Act
            var (glb, model) = Convert(bytes);

            // Assert
            Assert.Null(GlbTestReader.Json(glb)["extensions"]?["KHR_lights_punctual"]);
            Assert.DoesNotContain(model.LogicalNodes, e => e.Name == "Light");
        }

        [Fact]
        public void Attach_PlacesLightAtLocation()
        {
            // Arrange
            var bytes = Light(2, location: [5, 6, 7]);

            // Act
            var (_, model) = Convert(bytes);

            // Assert
            Assert.Equal(new Vector3(5, 6, 7), LightNode(model).LocalTransform.Translation);
        }
    }
}
