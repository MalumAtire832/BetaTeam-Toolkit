using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Blocks
{
    public class NodeTypesTests
    {
        private static FinStreamBuilder WriteNiLight(FinStreamBuilder builder, uint linkId)
        {
            return builder.SizedString("NiLight").NiObject(linkId, "Omni01").NiAVObjectFields()
                .Floats(1, 2, 3)                // location
                .Floats(0, 0, -1)               // direction
                .Byte(1)                        // light switch
                .Floats(0.5f)                   // spot angle
                .Floats(2)                      // spot exponent
                .Floats(0.75f)                  // dimmer
                .Floats(0.1f, 0.2f, 0.3f)       // ambient
                .Floats(0.4f, 0.5f, 0.6f)       // diffuse
                .Floats(0.7f, 0.8f, 0.9f)       // specular
                .Floats(100)                    // attenuation distance
                .Floats(1.5f)                   // attenuation curve
                .Byte(1)                        // attenuation
                .Int32(2)                       // light type
                .Int32(2).UInt32(0x99).UInt32(0x98);    // illuminated nodes, ignored by the engine
        }

        [Fact]
        public void Read_ReturnsLightFields_WhenLightIsValid()
        {
            // Arrange
            var bytes = WriteNiLight(new FinStreamBuilder().Header(), 0x10).EndOfFile().ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var light = Assert.IsType<NiLight>(Assert.Single(file.Objects));
            Assert.Equal(new Vector3(1, 2, 3), light.Location);
            Assert.Equal(new Vector3(0, 0, -1), light.Direction);
            Assert.True(light.LightSwitch);
            Assert.Equal(0.5f, light.SpotAngle);
            Assert.Equal(2f, light.SpotExponent);
            Assert.Equal(0.75f, light.Dimmer);
            Assert.Equal(new FinColor3(0.1f, 0.2f, 0.3f), light.AmbientColor);
            Assert.Equal(new FinColor3(0.4f, 0.5f, 0.6f), light.DiffuseColor);
            Assert.Equal(new FinColor3(0.7f, 0.8f, 0.9f), light.SpecularColor);
            Assert.Equal(100f, light.AttenuationDistance);
            Assert.Equal(1.5f, light.AttenuationCurve);
            Assert.True(light.Attenuation);
            Assert.Equal(2, light.LightType);
            Assert.Equal([0x99u, 0x98u], light.IlluminatedNodes);
        }

        [Fact]
        public void Read_LinksNodeEffectToLight_WhenNodeListsLight()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("NiNode").NiObject(0x20).NiAVObjectFields().NiNodeFields([], [0x10]);
            var bytes = WriteNiLight(builder, 0x10).EndOfFile().ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var node = Assert.IsType<NiNode>(file.TopLevelObjects[0]);
            Assert.Same(file.Objects[1], Assert.Single(node.Effects).Target);
        }

        [Fact]
        public void Read_ReturnsLodRanges_WhenLodNodeIsValid()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("NiLODNode").NiObject(0x10).NiAVObjectFields().NiNodeFields([], [])
                .Int32(1)                           // active child index
                .Byte(1)                            // update only active child
                .Int32(2)                           // range count
                .Floats(0, 50).Floats(1, 2, 3)      // near, far, center
                .Floats(50, 200).Floats(4, 5, 6)
                .Byte(1)                            // position in range
                .EndOfFile()
                .ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var lod = Assert.IsType<NiLODNode>(Assert.Single(file.Objects));
            Assert.Equal(1, lod.ActiveChildIndex);
            Assert.True(lod.UpdateOnlyActiveChild);
            Assert.Equal(
                [new FinLodRange(0, 50, new Vector3(1, 2, 3)), new FinLodRange(50, 200, new Vector3(4, 5, 6))],
                lod.Ranges);
            Assert.True(lod.PositionInRange);
        }

        [Fact]
        public void Read_ReturnsBillboardMode_WhenBillboardNodeIsValid()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("NiBillboardNode").NiObject(0x10).NiAVObjectFields().NiNodeFields([], [])
                .Int32(3)                           // mode
                .EndOfFile()
                .ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var billboard = Assert.IsType<NiBillboardNode>(Assert.Single(file.Objects));
            Assert.Equal(3, billboard.Mode);
        }
    }
}
