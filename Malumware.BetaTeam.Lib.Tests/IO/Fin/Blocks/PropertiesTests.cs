using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Blocks
{
    public class PropertiesTests
    {
        private static T ReadSingle<T>(FinStreamBuilder builder)
            where T : NiObject
        {
            var file = FinReader.Read("TEST", builder.EndOfFile().ToArray());
            return Assert.IsType<T>(Assert.Single(file.Objects));
        }

        // Class name, NiObject part and the master flag every NiProperty starts with
        private static FinStreamBuilder Property(string className, uint linkId = 0x10)
        {
            return new FinStreamBuilder().Header().SizedString(className).NiObject(linkId).Byte(1);
        }

        [Fact]
        public void Read_ReturnsMaterial_WhenMaterialPropertyIsValid()
        {
            // Arrange
            var builder = Property("NiMaterialProperty")
                .Floats(0.1f, 0.2f, 0.3f)       // ambient
                .Floats(0.4f, 0.5f, 0.6f)       // diffuse
                .Floats(0.7f, 0.8f, 0.9f)       // specular
                .Floats(1, 0, 0)                // emittance
                .Floats(10)                     // shininess
                .Floats(0.5f);                  // alpha

            // Act
            var material = ReadSingle<NiMaterialProperty>(builder);

            // Assert
            Assert.True(material.Master);
            Assert.Equal(new FinColor3(0.1f, 0.2f, 0.3f), material.AmbientColor);
            Assert.Equal(new FinColor3(0.4f, 0.5f, 0.6f), material.DiffuseColor);
            Assert.Equal(new FinColor3(0.7f, 0.8f, 0.9f), material.SpecularColor);
            Assert.Equal(new FinColor3(1, 0, 0), material.Emittance);
            Assert.Equal(10f, material.Shininess);
            Assert.Equal(0.5f, material.Alpha);
        }

        [Fact]
        public void Read_ReturnsBlendSettings_WhenAlphaPropertyIsValid()
        {
            // Arrange
            var builder = Property("NiAlphaProperty").Byte(1).UInt32(6).UInt32(7);

            // Act
            var alpha = ReadSingle<NiAlphaProperty>(builder);

            // Assert
            Assert.True(alpha.AlphaBlending);
            Assert.Equal(6u, alpha.SourceBlendMode);
            Assert.Equal(7u, alpha.DestinationBlendMode);
        }

        [Fact]
        public void Read_ReturnsModes_WhenTextureModePropertyIsValid()
        {
            // Arrange
            var builder = Property("NiTextureModeProperty").UInt32(1).UInt32(2).UInt32(3);

            // Act
            var mode = ReadSingle<NiTextureModeProperty>(builder);

            // Assert
            Assert.Equal(1u, mode.ApplyMode);
            Assert.Equal(2u, mode.FilterMode);
            Assert.Equal(3u, mode.ClampMode);
        }

        [Fact]
        public void Read_ReturnsColorMode_WhenVertexColorPropertyIsValid()
        {
            // Arrange
            var builder = Property("NiVertexColorProperty").UInt32(2);

            // Act
            var vertexColor = ReadSingle<NiVertexColorProperty>(builder);

            // Assert
            Assert.Equal(2u, vertexColor.ColorMode);
        }

        [Fact]
        public void Read_ReturnsFlags_WhenZBufferPropertyIsValid()
        {
            // Arrange
            var builder = Property("NiZBufferProperty").Byte(1).Byte(0);

            // Act
            var zBuffer = ReadSingle<NiZBufferProperty>(builder);

            // Assert
            Assert.True(zBuffer.ZBufferTest);
            Assert.False(zBuffer.ZBufferWrite);
        }

        [Fact]
        public void Read_ReturnsSpecular_WhenSpecularPropertyIsValid()
        {
            // Arrange
            var builder = Property("NiSpecularProperty").Byte(1);

            // Act
            var specular = ReadSingle<NiSpecularProperty>(builder);

            // Assert
            Assert.True(specular.Master);
            Assert.True(specular.Specular);
        }

        [Fact]
        public void Read_ReadsShadePropertyWithoutMasterFlag()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("NiShadeProperty").NiObject(0x10)
                .Byte(1);                           // smooth; this class has no master flag

            // Act
            var shade = ReadSingle<NiShadeProperty>(builder);

            // Assert
            Assert.True(shade.Smooth);
            Assert.False(shade.Master);
        }

        [Fact]
        public void Read_ReturnsExternalImage_WhenImageNamesFile()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("NiImage").NiObject(0x10)
                .Byte(1)                            // external
                .CString("brick.tga")
                .UInt32(5);                         // preferred texture format

            // Act
            var image = ReadSingle<NiImage>(builder);

            // Assert
            Assert.True(image.External);
            Assert.Equal("brick.tga", image.FileName);
            Assert.True(image.RawData.IsNull);
            Assert.Equal(5u, image.PreferredTextureFormat);
        }

        [Fact]
        public void Read_ReturnsRawDataLink_WhenImageIsEmbedded()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header().SizedString("NiImage").NiObject(0x10)
                .Byte(0)                            // embedded
                .UInt32(0x99)                       // raw image data link
                .UInt32(0)
                .EndOfFile()
                .ToArray();

            // Act
            var act = () => FinReader.Read("TEST", bytes);

            // Assert: the raw data block isn't in the file, so linking fails on it
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("0x00000099", exception.Message);
        }

        [Fact]
        public void Read_LinksTexturePropertyImages_WhenImagesFollow()
        {
            // Arrange
            var bytes = Property("NiTextureProperty")
                .Int32(0)                           // index
                .Refs(0x20)                         // images
                .SizedString("NiImage").NiObject(0x20).Byte(1).CString("brick.tga").UInt32(0)
                .EndOfFile()
                .ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var texture = Assert.IsType<NiTextureProperty>(file.Objects[0]);
            Assert.Equal(0, texture.Index);
            Assert.Same(file.Objects[1], Assert.Single(texture.Images).Target);
        }

        [Fact]
        public void Read_ReturnsStageSettings_WhenMultiTexturePropertyIsValid()
        {
            // Arrange
            var bytes = Property("NiMultiTextureProperty")
                .Refs(0x20, 0x21)                   // images
                .Refs(1, 2)                         // combine modes
                .Refs(3, 4)                         // clamp modes
                .Refs(5, 6)                         // filter modes
                .SizedString("NiImage").NiObject(0x20).Byte(1).CString("a.tga").UInt32(0)
                .SizedString("NiImage").NiObject(0x21).Byte(1).CString("b.tga").UInt32(0)
                .EndOfFile()
                .ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var multi = Assert.IsType<NiMultiTextureProperty>(file.Objects[0]);
            Assert.Equal(2, multi.Images.Count);
            Assert.Same(file.Objects[2], multi.Images[1].Target);
            Assert.Equal([1u, 2u], multi.CombineModes);
            Assert.Equal([3u, 4u], multi.ClampModes);
            Assert.Equal([5u, 6u], multi.FilterModes);
        }

        [Fact]
        public void Read_ReturnsTiming_WhenFlipTexturesIsValid()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .SizedString("NiFlipTextures").NiObject(0x10)
                .UInt32(1)                          // out of bound
                .Floats(2, 3, 4)                    // rate, start time, cycle time
                .UInt32(0x20)                       // textures
                .SizedString("NiTextureProperty").NiObject(0x20).Byte(0).Int32(0).Refs()
                .EndOfFile()
                .ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var flip = Assert.IsType<NiFlipTextures>(file.Objects[0]);
            Assert.Equal(1u, flip.OutOfBound);
            Assert.Equal(2f, flip.Rate);
            Assert.Equal(3f, flip.StartTime);
            Assert.Equal(4f, flip.CycleTime);
            Assert.Same(file.Objects[1], flip.Textures.Target);
        }

        [Fact]
        public void Read_LinksNodeProperty_WhenPropertyFollows()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .TopLevel().SizedString("NiNode").NiObject(0x10).NiAVObjectFields(0x20).NiNodeFields([], [])
                .SizedString("NiZBufferProperty").NiObject(0x20).Byte(0).Byte(1).Byte(1)
                .EndOfFile()
                .ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var node = Assert.IsType<NiNode>(file.TopLevelObjects[0]);
            Assert.IsType<NiZBufferProperty>(Assert.Single(node.Properties).Target);
        }
    }
}
