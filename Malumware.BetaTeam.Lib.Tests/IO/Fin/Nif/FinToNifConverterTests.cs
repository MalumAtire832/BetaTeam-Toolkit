using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Nif;
using Malumware.BetaTeam.Lib.IO.Nif;
using Malumware.BetaTeam.Lib.IO.Nif.Blocks;
using Malumware.BetaTeam.Lib.IO.Nif.Blocks.BoundingVolumes;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Nif
{
    public class FinToNifConverterTests
    {
        private const uint PRESENT = 1;     // Optional arrays are preceded by their saved address; non-zero means present

        private static FinNifConversion Convert(FinStreamBuilder builder)
        {
            var file = FinReader.Read("TEST", builder.EndOfFile().ToArray());
            return new FinToNifConverter().Convert(file);
        }

        private static FinStreamBuilder Node(
            FinStreamBuilder builder, uint linkId, uint[] children, uint[]? properties = null, string className = "NiNode")
        {
            return builder.SizedString(className).NiObject(linkId, className + linkId)
                .NiAVObjectFields(properties ?? [])
                .NiNodeFields(children, []);
        }

        // A triangle with normals, colours and one texture set; triangle planes are left out
        private static FinStreamBuilder Shape(
            FinStreamBuilder builder, uint linkId, uint[]? properties = null, string className = "NiTriShape")
        {
            builder.SizedString(className).NiObject(linkId, "Shape" + linkId)
                .NiAVObjectFields(properties ?? [])
                .UInt16(3)
                .UInt32(PRESENT).Floats(0, 0, 0, 1, 0, 0, 0, 1, 0)
                .UInt32(PRESENT).Floats(0, 0, 1, 0, 0, 1, 0, 0, 1)
                .Floats(0.5f, 0.5f, 0, 1)
                .UInt16(className == "DDCorona" ? (ushort)0 : (ushort)1)
                .UInt16(1)
                .UInt32(PRESENT).Floats(0, 0, 0.5f, 1, 0, 0.5f, 0, 1, 0.5f)
                .UInt32(PRESENT).Floats(1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 1)
                .UInt32(0);
            return className == "DDCorona"
                ? builder.Floats(2.5f)
                : builder.UInt16(0).UInt16(1).UInt16(2);
        }

        private static FinStreamBuilder Image(FinStreamBuilder builder, uint linkId, string fileName)
        {
            return builder.SizedString("NiImage").NiObject(linkId).Byte(1).CString(fileName).UInt32(0);
        }

        private static IEnumerable<string> Strings(NiObjectNET block)
        {
            return block.EnumerateExtraData().OfType<NiStringExtraData>().Select(e => e.Value);
        }

        [Fact]
        public void Convert_CopiesTransformAndGeometry_WhenNodeHasShape()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("NiNode").NiObject(0x10, "Root")
                .Byte(1)                                    // app culled
                .Floats(1, 2, 3)                            // translation
                .Floats(0, 1, 0, -1, 0, 0, 0, 0, 1)         // rotation
                .Floats(2)                                  // scale
                .Floats(0, 0, 0)                            // velocity
                .Refs().UInt32(0).UInt32(0)
                .NiNodeFields([0x20], []);
            Shape(builder, 0x20);

            // Act
            var conversion = Convert(builder);

            // Assert
            var root = Assert.IsType<NiNode>(Assert.Single(conversion.File.Roots));
            Assert.Equal("Root", root.Name);
            Assert.Equal(NiAVObject.FLAG_HIDDEN, root.Flags);
            Assert.Equal(new Vector3(1, 2, 3), root.Translation);
            Assert.Equal(new NifMatrix33(0, 1, 0, -1, 0, 0, 0, 0, 1), root.Rotation);
            Assert.Equal(2f, root.Scale);
            Assert.Empty(Strings(root));

            var shape = Assert.IsType<NiTriShape>(Assert.Single(root.Children));
            var data = Assert.IsType<NiTriShapeData>(shape.Data);
            Assert.Equal(3, data.VertexCount);
            Assert.Equal(Vector3.UnitX, data.Vertices![1]);
            Assert.Equal(Vector3.UnitZ, data.Normals![2]);
            Assert.Equal(new NifColor4(0, 1, 0, 1), data.VertexColors![1]);
            Assert.Equal([Vector2.Zero, Vector2.UnitX, Vector2.UnitY], Assert.Single(data.UvSets));
            Assert.Equal(new NifTriangle(0, 1, 2), Assert.Single(data.Triangles));
            Assert.Empty(conversion.Warnings);
        }

        [Fact]
        public void Convert_AddsActorDataAsExtraData_WhenRootIsUnit()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("DDUnit").NiObject(0x10, "U0001")
                .NiAVObjectFields().NiNodeFields([], []).UInt32(0x20)
                .SizedString("DDActorSharedData").NiObject(0x20, "U0001")
                .CString("Catapult").CString("u0165").Byte(1).Byte(0).Floats(0.5f).UInt32(0)
                .Int32(2)
                .CString("neutral").Floats(0, 960).Byte(0).Int32(0).Int32(0).Int32(0)
                .CString("fire").Floats(960, 3200).Byte(1)
                .CString("boom.dds").CString(null).Byte(0).Byte(0).Byte(0).Floats(0, 1, 1, 100, 10)
                .Int32(0).Int32(0).Int32(0)
                .Int32(0)                                   // tracks
                .UInt32(1).Floats(1, 2, 3);                 // floor points

            // Act
            var conversion = Convert(builder);

            // Assert
            var root = Assert.IsType<NiNode>(Assert.Single(conversion.File.Roots));
            Assert.Equal(
                [
                    "FinClass: DDUnit",
                    "Type: U0001",
                    "Description: Catapult",
                    "Behavior: u0165",
                    "MakeShadow: true",
                    "Prop: false",
                    "ShadowMultiplier: 0.5",
                    "FloorPoints: (1 2 3)",
                    "SkillSound: fire: boom.dds",
                ],
                Strings(root)
            );
            var keys = Assert.Single(root.EnumerateExtraData().OfType<NiTextKeyExtraData>());
            Assert.Equal(
                [
                    new NifTextKey(0, "neutral: start"),
                    new NifTextKey(960, "neutral: stop"),
                    new NifTextKey(960, "fire: start"),
                    new NifTextKey(3200, "fire: stop"),
                ],
                keys.TextKeys
            );
            Assert.Empty(conversion.Warnings);
        }

        [Fact]
        public void Convert_CombinesInheritedTextureWithOwnMode_WhenModeIsOnChild()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            Node(builder, 0x10, [0x20], [0x30]);
            Shape(builder, 0x20, [0x40]);
            builder.SizedString("NiTextureProperty").NiObject(0x30).Byte(0).Int32(1).Refs(0x50, 0x51);
            builder.SizedString("NiTextureModeProperty").NiObject(0x40).Byte(0).UInt32(0).UInt32(1).UInt32(0);
            Image(builder, 0x50, "a.tga");
            Image(builder, 0x51, "b.tga");

            // Act
            var conversion = Convert(builder);

            // Assert
            var root = Assert.IsType<NiNode>(Assert.Single(conversion.File.Roots));
            var rootTexturing = Assert.IsType<NiTexturingProperty>(Assert.Single(root.Properties));
            var rootBase = rootTexturing.Textures[(int)NifTextureSlot.Base]!;
            Assert.Equal("b.tga", rootBase.Source!.FileName);
            Assert.Equal(NifTexDesc.CLAMP_WRAP_S_WRAP_T, rootBase.ClampMode);
            Assert.Equal(NiTexturingProperty.APPLY_MODULATE, rootTexturing.ApplyMode);
            Assert.Equal(["Images: a.tga, b.tga"], Strings(rootTexturing));

            var shape = Assert.IsType<NiTriShape>(Assert.Single(root.Children));
            var shapeTexturing = Assert.IsType<NiTexturingProperty>(Assert.Single(shape.Properties));
            Assert.NotSame(rootTexturing, shapeTexturing);
            var shapeBase = shapeTexturing.Textures[(int)NifTextureSlot.Base]!;
            Assert.Same(rootBase.Source, shapeBase.Source);
            Assert.Equal(0u, shapeTexturing.ApplyMode);
            Assert.Equal(1u, shapeBase.FilterMode);
            Assert.Equal(0u, shapeBase.ClampMode);
        }

        [Fact]
        public void Convert_FillsSlotsInStageOrder_WhenShapeHasMultiTexture()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            Shape(builder, 0x10, [0x20]);
            builder.SizedString("NiMultiTextureProperty").NiObject(0x20).Byte(0)
                .Refs(0x30, 0x31)
                .UInt32(2).UInt32(0).UInt32(5)              // combine modes
                .UInt32(2).UInt32(3).UInt32(0)              // clamp modes
                .UInt32(2).UInt32(2).UInt32(1);             // filter modes
            Image(builder, 0x30, "base.tga");
            Image(builder, 0x31, "light.tga");

            // Act
            var conversion = Convert(builder);

            // Assert
            var shape = Assert.IsType<NiTriShape>(Assert.Single(conversion.File.Roots));
            var texturing = Assert.IsType<NiTexturingProperty>(Assert.Single(shape.Properties));
            var baseTexture = texturing.Textures[(int)NifTextureSlot.Base]!;
            Assert.Equal("base.tga", baseTexture.Source!.FileName);
            Assert.Equal((3u, 2u, 0u), (baseTexture.ClampMode, baseTexture.FilterMode, baseTexture.UvSet));
            var dark = texturing.Textures[(int)NifTextureSlot.Dark]!;
            Assert.Equal("light.tga", dark.Source!.FileName);
            Assert.Equal((0u, 1u, 1u), (dark.ClampMode, dark.FilterMode, dark.UvSet));
            Assert.Equal(["CombineModes: 0, 5"], Strings(texturing));
        }

        [Fact]
        public void Convert_ConvertsPropertiesOnce_WhenTwoShapesShareThem()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            Node(builder, 0x10, [0x20, 0x21]);
            Shape(builder, 0x20, [0x30, 0x31]);
            Shape(builder, 0x21, [0x30]);
            builder.SizedString("NiMaterialProperty").NiObject(0x30, "Red").Byte(0)
                .Floats(0.1f, 0.1f, 0.1f, 1, 0, 0, 1, 1, 1, 0, 0, 0)
                .Floats(20, 0.5f);
            builder.SizedString("NiAlphaProperty").NiObject(0x31).Byte(0).Byte(1).UInt32(6).UInt32(7);

            // Act
            var conversion = Convert(builder);

            // Assert
            var root = Assert.IsType<NiNode>(Assert.Single(conversion.File.Roots));
            var first = Assert.IsType<NiTriShape>(root.Children[0]);
            var second = Assert.IsType<NiTriShape>(root.Children[1]);
            var material = Assert.IsType<NiMaterialProperty>(first.Properties[0]);
            Assert.Same(material, Assert.Single(second.Properties));
            Assert.Equal("Red", material.Name);
            Assert.Equal(new NifColor3(1, 0, 0), material.DiffuseColor);
            Assert.Equal((20f, 0.5f), (material.Glossiness, material.Alpha));
            var alpha = Assert.IsType<NiAlphaProperty>(first.Properties[1]);
            Assert.Equal(0x00ED, alpha.Flags);         // blending, source alpha, inverse source alpha
        }

        [Fact]
        public void Convert_KeepsChildSlots_WhenLodNodeHasEmptyChildAndLight()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            Node(builder, 0x10, [0, 0x20, 0x30], className: "NiLODNode")
                .Int32(1).Byte(1)                           // active child, update only active child
                .UInt32(3)
                .Floats(0, 10, 0, 0, 0)
                .Floats(10, 50, 0, 0, 0)
                .Floats(50, 100, 0, 0, 0)
                .Byte(0);
            Shape(builder, 0x20);
            builder.SizedString("NiLight").NiObject(0x30).NiAVObjectFields()
                .Floats(0, 0, 0, 0, 0, -1).Byte(1).Floats(45, 1, 1)
                .Floats(0, 0, 0, 1, 1, 1, 1, 1, 1).Floats(100, 1).Byte(1).Int32(2).UInt32(0);

            // Act
            var conversion = Convert(builder);

            // Assert
            var lod = Assert.IsType<NiLODNode>(Assert.Single(conversion.File.Roots));
            Assert.Equal(3, lod.Children.Count);
            Assert.Null(lod.Children[0]);
            Assert.IsType<NiTriShape>(lod.Children[1]);
            Assert.Null(lod.Children[2]);
            Assert.Equal(1u, lod.Index);
            Assert.Equal([new(0, 10), new(10, 50), new(50, 100)], lod.LodLevels);
            Assert.Equal(Vector3.Zero, lod.LodCenter);
            Assert.Empty(Strings(lod));
            Assert.Contains("NiLight isn't exported: light types aren't mapped yet", conversion.Warnings);
        }

        [Fact]
        public void Convert_ListsEveryCenter_WhenLodRangesHaveDifferentCenters()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            Node(builder, 0x10, [], className: "NiLODNode")
                .Int32(0).Byte(0)
                .UInt32(2).Floats(0, 10, 1, 2, 3).Floats(10, 20, 4, 5, 6)
                .Byte(0);

            // Act
            var conversion = Convert(builder);

            // Assert
            var lod = Assert.IsType<NiLODNode>(Assert.Single(conversion.File.Roots));
            Assert.Equal(new Vector3(1, 2, 3), lod.LodCenter);
            Assert.Equal(["LodCenters: (1 2 3), (4 5 6)"], Strings(lod));
            Assert.Single(conversion.Warnings);
        }

        [Theory]
        [InlineData(0, 0x0000)]
        [InlineData(1, 0x0020)]
        [InlineData(2, 0x0040)]
        public void Convert_StoresBillboardModeInFlags_WhenModeIsKnown(int mode, ushort flags)
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            Node(builder, 0x10, [], className: "NiBillboardNode").Int32(mode);

            // Act
            var conversion = Convert(builder);

            // Assert
            var node = Assert.IsType<NiBillboardNode>(Assert.Single(conversion.File.Roots));
            Assert.Equal(flags, node.Flags);
            Assert.Empty(conversion.Warnings);
        }

        [Fact]
        public void Convert_ConvertsVolume_WhenUnionHoldsSphereAndBox()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("NiNode").NiObject(0x10)
                .Byte(0).Floats(0, 0, 0).Floats(1, 0, 0, 0, 1, 0, 0, 0, 1).Floats(1).Floats(0, 0, 0)
                .Refs().UInt32(0)
                .UInt32(1)                                  // has bounding volume
                .UInt32(4).UInt32(2)                        // union of two
                .UInt32(0).Floats(1, 2, 3, 4).Byte(0)       // sphere
                .UInt32(1).Floats(0, 0, 0).Floats(1, 0, 0, 0, 1, 0, 0, 0, 1).Floats(1, 2, 3).Byte(0)
                .NiNodeFields([], []);

            // Act
            var conversion = Convert(builder);

            // Assert
            var node = Assert.IsType<NiNode>(Assert.Single(conversion.File.Roots));
            var union = Assert.IsType<NifUnionBV>(node.BoundingVolume);
            var sphere = Assert.IsType<NifSphereBV>(union.Volumes[0]);
            Assert.Equal((new Vector3(1, 2, 3), 4f), (sphere.Center, sphere.Radius));
            var box = Assert.IsType<NifBoxBV>(union.Volumes[1]);
            Assert.Equal(new Vector3(1, 2, 3), box.Extents);
            Assert.Empty(conversion.Warnings);
        }

        [Fact]
        public void Convert_DescribesVolume_WhenShapeHasNoNifEquivalent()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString("NiNode").NiObject(0x10)
                .Byte(0).Floats(0, 0, 0).Floats(1, 0, 0, 0, 1, 0, 0, 0, 1).Floats(1).Floats(0, 0, 0)
                .Refs().UInt32(0)
                .UInt32(1)
                .UInt32(2).Floats(0, 0, 0).Floats(0, 0, 1).Floats(0.5f).Byte(1)     // inverted capsule
                .NiNodeFields([], []);

            // Act
            var conversion = Convert(builder);

            // Assert
            var node = Assert.IsType<NiNode>(Assert.Single(conversion.File.Roots));
            Assert.Null(node.BoundingVolume);
            Assert.Equal(["BoundingVolume: capsule(origin (0 0 0), direction (0 0 1), radius 0.5, inverted)"], Strings(node));
            Assert.Single(conversion.Warnings);
        }

        [Fact]
        public void Convert_KeepsCoronaSizeWithoutTriangles_WhenShapeIsCorona()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            Shape(builder, 0x10, className: "DDCorona");

            // Act
            var conversion = Convert(builder);

            // Assert
            var shape = Assert.IsType<NiTriShape>(Assert.Single(conversion.File.Roots));
            Assert.Equal(["FinClass: DDCorona", "CoronaSize: 2.5"], Strings(shape));
            var data = Assert.IsType<NiTriShapeData>(shape.Data);
            Assert.Equal(3, data.VertexCount);
            Assert.Empty(data.Triangles);
        }

        [Fact]
        public void Convert_WarnsAboutAnimation_WhenFileHasFlipTextures()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            Shape(builder, 0x10, [0x20]);
            builder.SizedString("NiTextureProperty").NiObject(0x20).Byte(0).Int32(0).Refs(0x30);
            Image(builder, 0x30, "a.tga");
            builder.SizedString("NiFlipTextures").NiObject(0x40).UInt32(0).Floats(1, 0, 1).UInt32(0x20);

            // Act
            var conversion = Convert(builder);

            // Assert
            Assert.Equal(["NiFlipTextures isn't exported: texture animation isn't supported yet"], conversion.Warnings);
        }

        [Fact]
        public void Convert_CutsLoop_WhenNodeIsItsOwnChild()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            Node(builder, 0x10, [0x20]);
            Node(builder, 0x20, [0x10]);

            // Act
            var conversion = Convert(builder);

            // Assert
            var root = Assert.IsType<NiNode>(Assert.Single(conversion.File.Roots));
            var child = Assert.IsType<NiNode>(Assert.Single(root.Children));
            Assert.Null(Assert.Single(child.Children));
            Assert.Single(conversion.Warnings);
            NifWriter.Write(conversion.File);
        }

        [Fact]
        public void Convert_Throws_WhenSceneIsNestedTooDeep()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            const uint count = FinToNifConverter.MAX_DEPTH + 2;
            for (uint i = 1; i <= count; i++)
            {
                Node(builder, i, i < count ? [i + 1] : []);
            }

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => Convert(builder));
        }
    }
}
