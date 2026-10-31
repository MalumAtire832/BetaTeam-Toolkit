using System.Numerics;
using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;
using SharpGLTF.Schema2;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    public class FinGltfMaterialsTests
    {
        private const uint PRESENT = 0x0A96AC58;    // Optional arrays are preceded by a non-zero address when present

        private static FinStreamBuilder Start()
        {
            return new FinStreamBuilder().Header().TopLevel();
        }

        private static FinStreamBuilder Node(FinStreamBuilder builder, uint linkId, uint[] children, uint[]? properties = null)
        {
            return builder.SizedString("NiNode").NiObject(linkId, $"Node{linkId:X}")
                .NiAVObjectFields(properties ?? [])
                .NiNodeFields(children, []);
        }

        // A triangle with two texture sets and vertex colours; normals only when lit
        private static FinStreamBuilder Shape(FinStreamBuilder builder, uint linkId, uint[] properties, bool normals = true)
        {
            builder = builder.SizedString("NiTriShape").NiObject(linkId, $"Shape{linkId:X}")
                .NiAVObjectFields(properties)
                .UInt16(3)                                                  // vertex count
                .UInt32(PRESENT).Floats(0, 0, 0, 1, 0, 0, 0, 1, 0);         // vertices
            builder = normals
                ? builder.UInt32(PRESENT).Floats(0, 0, 1, 0, 0, 1, 0, 0, 1)
                : builder.UInt32(0);

            return builder
                .Floats(0.5f, 0.5f, 0, 0.75f)                               // bound
                .UInt16(1)                                                  // triangle count
                .UInt16(2)                                                  // texture set count
                .UInt32(PRESENT).Floats(0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0)
                .UInt32(PRESENT).Floats(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1) // colours
                .UInt32(0)                                                  // no triangle planes
                .UInt16(0).UInt16(1).UInt16(2);
        }

        private static FinStreamBuilder Image(FinStreamBuilder builder, uint linkId, string fileName)
        {
            return builder.SizedString("NiImage").NiObject(linkId).Byte(1).CString(fileName).UInt32(0);
        }

        private static FinStreamBuilder Texture(FinStreamBuilder builder, uint linkId, int index, params uint[] images)
        {
            return builder.SizedString("NiTextureProperty").NiObject(linkId).Byte(1).Int32(index).Refs(images);
        }

        private static FinStreamBuilder TextureMode(FinStreamBuilder builder, uint linkId, uint filter, uint clamp)
        {
            return builder.SizedString("NiTextureModeProperty").NiObject(linkId).Byte(1).UInt32(3).UInt32(filter).UInt32(clamp);
        }

        private static FinStreamBuilder Material(FinStreamBuilder builder, uint linkId, float[] diffuse, float alpha, float[]? emittance = null)
        {
            return builder.SizedString("NiMaterialProperty").NiObject(linkId).Byte(1)
                .Floats(0.1f, 0.1f, 0.1f)                                   // ambient
                .Floats(diffuse)
                .Floats(0, 0, 0)                                            // specular
                .Floats(emittance ?? [0, 0, 0])
                .Floats(0.5f, alpha);
        }

        private static FinStreamBuilder Alpha(FinStreamBuilder builder, uint linkId)
        {
            return builder.SizedString("NiAlphaProperty").NiObject(linkId).Byte(1).Byte(1).UInt32(6).UInt32(7);
        }

        private static FinStreamBuilder Lightmap(FinStreamBuilder builder, uint linkId, uint image)
        {
            return builder.SizedString("NiMultiTextureProperty").NiObject(linkId).Byte(1)
                .Refs(image)
                .UInt32(1).UInt32(0)                                        // combine modes
                .UInt32(1).UInt32(0)                                        // clamp modes
                .UInt32(1).UInt32(1);                                       // filter modes
        }

        private static FinStreamBuilder ZBuffer(FinStreamBuilder builder, uint linkId)
        {
            return builder.SizedString("NiZBufferProperty").NiObject(linkId).Byte(1).Byte(0).Byte(0);
        }

        private static byte[] ToGlb(FinStreamBuilder builder)
        {
            return new FinToGltfConverter().ToGlb(FinReader.Read("TEST", builder.EndOfFile().ToArray()));
        }

        private static ModelRoot Convert(FinStreamBuilder builder)
        {
            return GlbTestReader.Read(ToGlb(builder));
        }

        private static ModelRoot Convert(FinStreamBuilder builder, out IReadOnlyList<string> imageUris)
        {
            var glb = ToGlb(builder);
            imageUris = GlbTestReader.ImageUris(glb);

            return GlbTestReader.Read(glb);
        }

        private static Material ShapeMaterial(ModelRoot model, string name)
        {
            var node = model.LogicalNodes.Single(e => e.Name == name);

            return Assert.Single(node.Mesh.Primitives).Material;
        }

        private static string? ImageUri(IReadOnlyList<string> imageUris, Image? image)
        {
            return image is null ? null : imageUris[image.LogicalIndex];
        }

        [Fact]
        public void ToGlb_ReferencesBaseTextureByRelativeUri_WhenShapeHasTextureProperty()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30]);
            builder = Texture(builder, 0x30, 0, 0x40);
            builder = Image(builder, 0x40, "Studs Gry.tga");

            // Act
            var json = GlbTestReader.Json(ToGlb(builder));

            // Assert
            var image = Assert.Single(json["images"]!.AsArray())!.AsObject();
            Assert.Equal("../textures/studs_gry.png", image["uri"]!.GetValue<string>());
            Assert.False(image.ContainsKey("bufferView"));
            var baseColor = json["materials"]![0]!["pbrMetallicRoughness"]!["baseColorTexture"]!.AsObject();
            Assert.Equal(0, baseColor["index"]!.GetValue<int>());
            Assert.False(baseColor.ContainsKey("texCoord"));        // texture set 0 is the default
        }

        [Fact]
        public void ToModel_InheritsPropertiesFromAncestors_WhenShapeHasNone()
        {
            // Arrange
            var builder = Node(Start(), 0x10, [0x20], [0x30]);
            builder = Shape(builder, 0x20, []);
            builder = Texture(builder, 0x30, 0, 0x40);
            builder = Image(builder, 0x40, "Parent.tga");

            // Act
            var material = ShapeMaterial(Convert(builder, out var uris), "Shape20");

            // Assert
            var texture = material.FindChannel("BaseColor")!.Value.Texture;
            Assert.Equal("../textures/parent.png", ImageUri(uris, texture?.PrimaryImage));
        }

        [Fact]
        public void ToModel_UsesLowestProperty_WhenAncestorAndShapeHaveSameClass()
        {
            // Arrange
            var builder = Node(Start(), 0x10, [0x20], [0x30]);
            builder = Shape(builder, 0x20, [0x31]);
            builder = Texture(builder, 0x30, 0, 0x40);
            builder = Texture(builder, 0x31, 0, 0x41);
            builder = Image(builder, 0x40, "Parent.tga");
            builder = Image(builder, 0x41, "Own.tga");

            // Act
            var material = ShapeMaterial(Convert(builder, out var uris), "Shape20");

            // Assert
            var texture = material.FindChannel("BaseColor")!.Value.Texture;
            Assert.Equal("../textures/own.png", ImageUri(uris, texture?.PrimaryImage));
        }

        [Theory]
        [InlineData(0u, TextureInterpolationFilter.NEAREST, TextureMipMapFilter.NEAREST)]
        [InlineData(1u, TextureInterpolationFilter.LINEAR, TextureMipMapFilter.LINEAR)]
        [InlineData(2u, TextureInterpolationFilter.LINEAR, TextureMipMapFilter.LINEAR_MIPMAP_LINEAR)]
        [InlineData(3u, TextureInterpolationFilter.NEAREST, TextureMipMapFilter.NEAREST_MIPMAP_NEAREST)]
        [InlineData(4u, TextureInterpolationFilter.NEAREST, TextureMipMapFilter.NEAREST_MIPMAP_LINEAR)]
        [InlineData(5u, TextureInterpolationFilter.LINEAR, TextureMipMapFilter.LINEAR_MIPMAP_NEAREST)]
        public void ToModel_SetsSamplerFilter_FromTextureMode(uint filter, TextureInterpolationFilter mag, TextureMipMapFilter min)
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30, 0x31]);
            builder = Texture(builder, 0x30, 0, 0x40);
            builder = TextureMode(builder, 0x31, filter, 3);
            builder = Image(builder, 0x40, "A.tga");

            // Act
            var sampler = ShapeMaterial(Convert(builder), "Shape20").FindChannel("BaseColor")!.Value.TextureSampler;

            // Assert
            Assert.Equal(mag, sampler.MagFilter);
            Assert.Equal(min, sampler.MinFilter);
        }

        [Theory]
        [InlineData(0u, TextureWrapMode.CLAMP_TO_EDGE, TextureWrapMode.CLAMP_TO_EDGE)]
        [InlineData(1u, TextureWrapMode.CLAMP_TO_EDGE, TextureWrapMode.REPEAT)]
        [InlineData(2u, TextureWrapMode.REPEAT, TextureWrapMode.CLAMP_TO_EDGE)]
        [InlineData(3u, TextureWrapMode.REPEAT, TextureWrapMode.REPEAT)]
        public void ToModel_SetsSamplerWrap_FromTextureMode(uint clamp, TextureWrapMode wrapS, TextureWrapMode wrapT)
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30, 0x31]);
            builder = Texture(builder, 0x30, 0, 0x40);
            builder = TextureMode(builder, 0x31, 1, clamp);
            builder = Image(builder, 0x40, "A.tga");

            // Act
            var sampler = ShapeMaterial(Convert(builder), "Shape20").FindChannel("BaseColor")!.Value.TextureSampler;

            // Assert
            Assert.Equal(wrapS, sampler.WrapS);
            Assert.Equal(wrapT, sampler.WrapT);
        }

        [Fact]
        public void ToModel_SetsBaseColorAndEmissive_WhenLitShapeHasMaterialProperty()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30]);
            builder = Material(builder, 0x30, [0.2f, 0.4f, 0.6f], 0.5f, [0.1f, 0.2f, 0.3f]);

            // Act
            var material = ShapeMaterial(Convert(builder), "Shape20");

            // Assert
            Assert.False(material.Unlit);
            Assert.Equal(new Vector4(0.2f, 0.4f, 0.6f, 0.5f), material.FindChannel("BaseColor")!.Value.Color);
            Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), Xyz(material.FindChannel("Emissive")!.Value.Color));
        }

        [Fact]
        public void ToModel_ClampsAlpha_WhenAboveOne()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30]);
            builder = Material(builder, 0x30, [1, 1, 1], 3.6111114f);

            // Act
            var material = ShapeMaterial(Convert(builder), "Shape20");

            // Assert
            Assert.Equal(1, material.FindChannel("BaseColor")!.Value.Color.W);
        }

        [Fact]
        public void ToModel_MarksMaterialUnlitWithWhiteBase_WhenShapeHasNoNormals()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30], normals: false);
            builder = Material(builder, 0x30, [0.2f, 0.4f, 0.6f], 0.5f, [0.1f, 0.2f, 0.3f]);

            // Act
            var material = ShapeMaterial(Convert(builder), "Shape20");

            // Assert
            Assert.True(material.Unlit);
            Assert.Equal(new Vector4(1, 1, 1, 0.5f), material.FindChannel("BaseColor")!.Value.Color);
            var emissive = material.FindChannel("Emissive")?.Color ?? Vector4.Zero;
            Assert.Equal(Vector3.Zero, Xyz(emissive));
        }

        [Fact]
        public void ToModel_UsesBlend_WhenAlphaPropertyBlends()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30]);
            builder = Alpha(builder, 0x30);

            // Act
            var material = ShapeMaterial(Convert(builder), "Shape20");

            // Assert
            Assert.Equal(AlphaMode.BLEND, material.Alpha);
        }

        [Fact]
        public void ToModel_UsesOpaque_WhenNoAlphaProperty()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30]);
            builder = Material(builder, 0x30, [1, 1, 1], 0.5f);

            // Act
            var material = ShapeMaterial(Convert(builder), "Shape20");

            // Assert
            Assert.Equal(AlphaMode.OPAQUE, material.Alpha);
        }

        [Fact]
        public void ToModel_AddsLightmapImageToExtras_WhenShapeHasMultiTexture()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30], normals: false);
            builder = Lightmap(builder, 0x30, 0x40);
            builder = Image(builder, 0x40, "e1010LM0066rc.tga");

            // Act
            var model = Convert(builder, out var uris);

            // Assert
            var extras = Assert.IsType<JsonObject>(ShapeMaterial(model, "Shape20").Extras);
            var lightmap = extras["lightmap"]!.AsObject();
            var image = model.LogicalImages[lightmap["image"]!.GetValue<int>()];
            Assert.Equal("../textures/e1010lm0066rc.png", ImageUri(uris, image));
            Assert.Equal(1, lightmap["texCoord"]!.GetValue<int>());
        }

        [Fact]
        public void ToModel_KeepsEffectivePropertiesInMaterialExtras()
        {
            // Arrange
            var builder = Node(Start(), 0x10, [0x20], [0x30, 0x31]);
            builder = Shape(builder, 0x20, [0x32, 0x33]);
            builder = ZBuffer(builder, 0x30);
            builder = Material(builder, 0x31, [1, 1, 1], 1);
            builder = Material(builder, 0x32, [0.5f, 0.5f, 0.5f], 1);
            builder = Alpha(builder, 0x33);

            // Act
            var extras = Assert.IsType<JsonObject>(ShapeMaterial(Convert(builder), "Shape20").Extras);

            // Assert
            var properties = extras["properties"]!
                .AsArray()
                .Select(e => (e!["class"]!.GetValue<string>(), e["linkId"]!.GetValue<string>()))
                .ToList();
            Assert.Equal(
                [("NiZBufferProperty", "0x00000030"), ("NiMaterialProperty", "0x00000032"), ("NiAlphaProperty", "0x00000033")],
                properties
            );
        }

        [Fact]
        public void ToModel_SharesMaterial_WhenShapesShareProperties()
        {
            // Arrange
            var builder = Node(Start(), 0x10, [0x20, 0x21], [0x30]);
            builder = Shape(builder, 0x20, []);
            builder = Shape(builder, 0x21, []);
            builder = Alpha(builder, 0x30);

            // Act
            var model = Convert(builder);

            // Assert
            Assert.Same(ShapeMaterial(model, "Shape20"), ShapeMaterial(model, "Shape21"));
        }

        [Fact]
        public void ToModel_SeparatesMaterials_WhenOnlyUnlitDiffers()
        {
            // Arrange
            var builder = Node(Start(), 0x10, [0x20, 0x21], [0x30]);
            builder = Shape(builder, 0x20, []);
            builder = Shape(builder, 0x21, [], normals: false);
            builder = Alpha(builder, 0x30);

            // Act
            var model = Convert(builder);

            // Assert
            Assert.NotSame(ShapeMaterial(model, "Shape20"), ShapeMaterial(model, "Shape21"));
        }

        [Fact]
        public void ToModel_GivesEachPathItsMaterial_WhenShapeIsListedUnderParentsWithDifferentProperties()
        {
            // Arrange: one shape under two nodes, only one of which blends
            var builder = Node(Start(), 0x10, [0x11, 0x12]);
            builder = Node(builder, 0x11, [0x20], [0x30]);
            builder = Node(builder, 0x12, [0x20]);
            builder = Shape(builder, 0x20, []);
            builder = Alpha(builder, 0x30);

            // Act
            var model = Convert(builder);

            // Assert
            var materials = model.LogicalNodes
                .Where(e => e.Name == "Shape20")
                .Select(e => Assert.Single(e.Mesh.Primitives).Material.Alpha)
                .ToList();
            Assert.Equal([AlphaMode.BLEND, AlphaMode.OPAQUE], materials);
        }

        [Fact]
        public void ToModel_LeavesBaseTextureOut_WhenTextureIndexIsPastImages()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30]);
            builder = Texture(builder, 0x30, 5, 0x40);
            builder = Image(builder, 0x40, "A.tga");

            // Act
            var material = ShapeMaterial(Convert(builder), "Shape20");

            // Assert
            Assert.Null(material.FindChannel("BaseColor")!.Value.Texture);
        }

        [Fact]
        public void ToModel_LeavesBaseTextureOut_WhenImageListIsEmpty()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, [0x30]);
            builder = Texture(builder, 0x30, 0);

            // Act
            var material = ShapeMaterial(Convert(builder), "Shape20");

            // Assert
            Assert.Null(material.FindChannel("BaseColor")!.Value.Texture);
        }

        [Fact]
        public void ToModel_UsesPlainWhiteMaterial_WhenShapeHasNoProperties()
        {
            // Arrange
            var builder = Shape(Start(), 0x20, []);

            // Act
            var material = ShapeMaterial(Convert(builder), "Shape20");

            // Assert
            Assert.False(material.Unlit);
            Assert.Equal(AlphaMode.OPAQUE, material.Alpha);
            Assert.Equal(Vector4.One, material.FindChannel("BaseColor")!.Value.Color);
            Assert.Null(material.FindChannel("BaseColor")!.Value.Texture);
        }

        private static Vector3 Xyz(Vector4 v)
        {
            return new Vector3(v.X, v.Y, v.Z);
        }
    }
}
