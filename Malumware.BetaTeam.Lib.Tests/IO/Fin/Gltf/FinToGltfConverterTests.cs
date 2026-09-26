using System.Numerics;
using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    public class FinToGltfConverterTests
    {
        private const uint PRESENT = 0x0A96AC58;    // Optional arrays are preceded by a non-zero address when present

        private static FinStreamBuilder Node(
            FinStreamBuilder builder, uint linkId, string? name, uint[] children, uint[]? effects = null, uint[]? properties = null)
        {
            return builder.SizedString("NiNode").NiObject(linkId, name)
                .NiAVObjectFields(properties ?? [])
                .NiNodeFields(children, effects ?? []);
        }

        // A triangle (0, 1, 2) at the given position, with normals, colours and one texture set
        private static FinStreamBuilder Shape(
            FinStreamBuilder builder, uint linkId, string name, float[]? translation = null, float[]? normals = null,
            float[]? textureCoordinates = null, ushort lastIndex = 2)
        {
            return builder.SizedString("NiTriShape").NiObject(linkId, name)
                .Byte(0)
                .Floats(translation ?? [0, 0, 0])
                .Floats(1, 0, 0, 0, 1, 0, 0, 0, 1)
                .Floats(1)
                .Floats(0, 0, 0)
                .Refs()
                .UInt32(0)
                .UInt32(0)
                .UInt16(3)                                                  // vertex count
                .UInt32(PRESENT).Floats(0, 0, 0, 1, 0, 0, 0, 1, 0)          // vertices
                .UInt32(PRESENT).Floats(normals ?? [0, 0, 2, 0, 0, 1, 0, 0, 1])
                .Floats(0.5f, 0.5f, 0, 0.75f)                               // bound
                .UInt16(1)                                                  // triangle count
                .UInt16(1)                                                  // texture set count
                .UInt32(PRESENT).Floats(textureCoordinates ?? [0, 0, 0, 2, 0, 0, 0, -1, 0])
                .UInt32(PRESENT).Floats(1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0.5f)     // colours
                .UInt32(0)                                                  // no triangle planes
                .UInt16(0).UInt16(1).UInt16(lastIndex);
        }

        private static FinStreamBuilder Material(FinStreamBuilder builder, uint linkId)
        {
            return builder.SizedString("NiMaterialProperty").NiObject(linkId)
                .Byte(1)                                                    // master
                .Floats(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1, 0, 0)
                .Floats(10, 0.5f);
        }

        private static ModelRoot Convert(byte[] bytes, bool allLevelsOfDetail = false)
        {
            var glb = new FinToGltfConverter(allLevelsOfDetail).ToGlb(FinReader.Read("TEST", bytes));
            return ModelRoot.ParseGLB(glb, new ReadSettings { Validation = ValidationMode.Strict });
        }

        private static Node SceneRoot(ModelRoot model)
        {
            return Assert.Single(model.DefaultScene.VisualChildren);
        }

        private static JsonObject Extras(Node node)
        {
            return Assert.IsType<JsonObject>(node.Extras);
        }

        [Fact]
        public void ToGlb_KeepsHierarchyAndLocalTransforms_WhenNodeHasShape()
        {
            // Arrange
            var builder = Node(new FinStreamBuilder().Header().TopLevel(), 0x10, "Root", [0x20]);
            var bytes = Shape(builder, 0x20, "Box", translation: [1, 2, 3]).EndOfFile().ToArray();

            // Act
            var model = Convert(bytes);

            // Assert
            var sceneRoot = SceneRoot(model);
            Assert.Equal("TEST", sceneRoot.Name);
            Assert.Equal(FinGltfTransform.ZUpToYUp, sceneRoot.LocalTransform.Rotation);
            var root = Assert.Single(sceneRoot.VisualChildren);
            Assert.Equal("Root", root.Name);
            var box = Assert.Single(root.VisualChildren);
            Assert.Equal("Box", box.Name);
            Assert.Equal(new Vector3(1, 2, 3), box.LocalTransform.Translation);

            var primitive = Assert.Single(box.Mesh.Primitives);
            Assert.Equal(PrimitiveType.TRIANGLES, primitive.DrawPrimitiveType);
            var positions = primitive
                .GetVertexAccessor("POSITION")
                .AsVector3Array();
            Assert.Equal([Vector3.Zero, Vector3.UnitX, Vector3.UnitY], positions);
            Assert.Equal([0u, 1u, 2u], primitive.GetIndices());
            var extras = Extras(box);
            Assert.False(extras.ContainsKey("Vertices"));
            Assert.Equal(0.75f, extras["BoundRadius"]!.GetValue<float>());
        }

        [Fact]
        public void ToGlb_WritesNormalsColorsAndTextureCoordinates_WhenShapeHasThem()
        {
            // Arrange
            var bytes = Shape(new FinStreamBuilder().Header().TopLevel(), 0x20, "Box").EndOfFile().ToArray();

            // Act
            var primitive = Assert.Single(Convert(bytes).LogicalMeshes[0].Primitives);

            // Assert
            var normals = primitive
                .GetVertexAccessor("NORMAL")
                .AsVector3Array();
            var colors = primitive
                .GetVertexAccessor("COLOR_0")
                .AsVector4Array();
            var coordinates = primitive
                .GetVertexAccessor("TEXCOORD_0")
                .AsVector2Array();
            Assert.Equal([Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ], normals);
            Assert.Equal(new Vector4(0, 0, 1, 0.5f), colors[2]);
            Assert.Equal([Vector2.Zero, new Vector2(2, 0), new Vector2(0, -1)], coordinates);
            Assert.NotNull(primitive.Material);
        }

        [Fact]
        public void ToGlb_LeavesNormalsOut_WhenANormalIsZero()
        {
            // Arrange
            var bytes = Shape(new FinStreamBuilder().Header().TopLevel(), 0x20, "Box", normals: [0, 0, 0, 0, 0, 1, 0, 0, 1])
                .EndOfFile().ToArray();

            // Act
            var primitive = Assert.Single(Convert(bytes).LogicalMeshes[0].Primitives);

            // Assert
            Assert.Null(primitive.GetVertexAccessor("NORMAL"));
            Assert.NotNull(primitive.GetVertexAccessor("POSITION"));
        }

        [Fact]
        public void ToGlb_ReplacesTextureCoordinateWithZero_WhenItIsNotFinite()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().TopLevel();
            var textureCoordinates = new[] { float.NaN, 0, 0, 2, float.PositiveInfinity, 0, 0, -1, float.NaN };
            var bytes = Shape(builder, 0x20, "Box", textureCoordinates: textureCoordinates).EndOfFile().ToArray();

            // Act
            var model = Convert(bytes);

            // Assert
            var coordinates = Assert.Single(model.LogicalMeshes[0].Primitives)
                .GetVertexAccessor("TEXCOORD_0")
                .AsVector2Array();
            Assert.Equal([Vector2.Zero, new Vector2(2, 0), new Vector2(0, -1)], coordinates);
            var box = Assert.Single(SceneRoot(model).VisualChildren);
            Assert.Equal(2, Extras(box)["NonFiniteTextureCoordinates"]!.GetValue<int>());
        }

        [Fact]
        public void ToGlb_SharesMesh_WhenShapeIsListedTwice()
        {
            // Arrange
            var builder = Node(new FinStreamBuilder().Header().TopLevel(), 0x10, "Root", [0x20, 0x20]);
            var bytes = Shape(builder, 0x20, "Box").EndOfFile().ToArray();

            // Act
            var model = Convert(bytes);

            // Assert
            var root = Assert.Single(SceneRoot(model).VisualChildren);
            Assert.Equal(2, root.VisualChildren.Count());
            Assert.Single(model.LogicalMeshes);
            Assert.All(root.VisualChildren, e => Assert.Same(model.LogicalMeshes[0], e.Mesh));
        }

        [Fact]
        public void ToGlb_ThrowsInvalidData_WhenNodeIsItsOwnDescendant()
        {
            // Arrange
            var builder = Node(new FinStreamBuilder().Header().TopLevel(), 0x10, "Root", [0x20]);
            var bytes = Node(builder, 0x20, "Loop", [0x10]).EndOfFile().ToArray();
            var file = FinReader.Read("TEST", bytes);

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => new FinToGltfConverter().ToGlb(file));
        }

        [Fact]
        public void ToGlb_ThrowsInvalidData_WhenTriangleIsPastVertexCount()
        {
            // Arrange
            var bytes = Shape(new FinStreamBuilder().Header().TopLevel(), 0x20, "Box", lastIndex: 3).EndOfFile().ToArray();
            var file = FinReader.Read("TEST", bytes);

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => new FinToGltfConverter().ToGlb(file));
        }

        // Level 0 is shown from 50 to 100, level 1 from 0 to 50, so level 1 is the most detailed
        private static byte[] LodFile()
        {
            var builder = new FinStreamBuilder().Header().TopLevel()
                .SizedString("NiLODNode").NiObject(0x10, "Lod").NiAVObjectFields().NiNodeFields([0x20, 0x30], [])
                .Int32(0)                                       // active child
                .Byte(0)                                        // update only active child
                .Int32(2)
                .Floats(50, 100, 0, 0, 0)
                .Floats(0, 50, 0, 0, 0)
                .Byte(0);                                       // position in range
            builder = Shape(builder, 0x20, "Far");
            return Shape(builder, 0x30, "Near").EndOfFile().ToArray();
        }

        [Fact]
        public void ToGlb_WritesOnlyMostDetailedLevel_ByDefault()
        {
            // Act
            var model = Convert(LodFile());

            // Assert
            var lod = Assert.Single(SceneRoot(model).VisualChildren);
            var level = Assert.Single(lod.VisualChildren);
            Assert.Equal("Near", level.Name);
            Assert.Equal(1, Extras(level)["lodLevel"]!.GetValue<int>());
            Assert.Equal(2, Extras(lod)["Ranges"]!.AsArray().Count);
        }

        [Fact]
        public void ToGlb_WritesEveryLevel_WhenAllLevelsOfDetailIsSet()
        {
            // Act
            var model = Convert(LodFile(), allLevelsOfDetail: true);

            // Assert
            var lod = Assert.Single(SceneRoot(model).VisualChildren);
            Assert.Equal(["Far", "Near"], lod.VisualChildren.Select(e => e.Name));
            Assert.Equal([0, 1], lod.VisualChildren.Select(e => Extras(e)["lodLevel"]!.GetValue<int>()));
        }

        [Fact]
        public void ToGlb_WritesFieldsToExtras_WithoutTransformOrHierarchy()
        {
            // Arrange
            var builder = Node(new FinStreamBuilder().Header().TopLevel(), 0x10, "Root", [], properties: [0x40]);
            var bytes = Material(builder, 0x40).EndOfFile().ToArray();

            // Act
            var extras = Extras(Assert.Single(SceneRoot(Convert(bytes)).VisualChildren));

            // Assert
            Assert.Equal("NiNode", extras["class"]!.GetValue<string>());
            Assert.Equal("0x00000010", extras["linkId"]!.GetValue<string>());
            Assert.Equal("Root", extras["Name"]!.GetValue<string>());
            Assert.Equal("Default", extras["SortingMode"]!.GetValue<string>());
            Assert.False(extras.ContainsKey("Translation"));
            Assert.False(extras.ContainsKey("Children"));

            var material = Assert.IsType<JsonObject>(Assert.Single(extras["Properties"]!.AsArray()));
            Assert.Equal("NiMaterialProperty", material["class"]!.GetValue<string>());
            Assert.Equal(10f, material["Shininess"]!.GetValue<float>());
            Assert.Equal(0.4f, material["DiffuseColor"]!["R"]!.GetValue<float>());
        }

        [Fact]
        public void ToGlb_WritesSharedDataToSceneRootExtras_WhenActorLinksIt()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header()
                .TopLevel().SizedString("DDUnit").NiObject(0x20, "Instance").NiAVObjectFields().NiNodeFields([], [])
                .UInt32(0x10)                                   // shared data
                .SizedString("DDActorSharedData").NiObject(0x10, "U0001")
                .CString("Test unit").CString(null)
                .Byte(1).Byte(0).Floats(0.5f).UInt32(0)
                .Int32(0).Int32(0).UInt32(0)                    // no skills, tracks or floor points
                .EndOfFile()
                .ToArray();

            // Act
            var sceneRoot = SceneRoot(Convert(bytes));

            // Assert
            var shared = Extras(sceneRoot)["blocks"]!["0x00000010"]!;
            Assert.Equal("DDActorSharedData", shared["class"]!.GetValue<string>());
            Assert.Equal("Test unit", shared["Description"]!.GetValue<string>());
            var reference = Extras(Assert.Single(sceneRoot.VisualChildren))["SharedData"]!;
            Assert.Equal("0x00000010", reference["ref"]!.GetValue<string>());
        }

        [Fact]
        public void ToGlb_PlacesLightUnderSceneRoot_WhenNodeListsItAsEffect()
        {
            // Arrange
            var builder = Node(new FinStreamBuilder().Header().TopLevel(), 0x10, "Root", [], effects: [0x50]);
            var bytes = builder.SizedString("NiLight").NiObject(0x50, "Omni01").NiAVObjectFields()
                .Floats(1, 2, 3, 0, 0, -1).Byte(1).Floats(0.5f, 2, 0.75f)
                .Floats(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f)
                .Floats(100, 1.5f).Byte(1).Int32(2)
                .Int32(0)                                       // illuminated nodes
                .EndOfFile()
                .ToArray();

            // Act
            var sceneRoot = SceneRoot(Convert(bytes));

            // Assert
            Assert.Equal(["Root", "Omni01"], sceneRoot.VisualChildren.Select(e => e.Name));
            var light = sceneRoot.VisualChildren.Last();
            Assert.Null(light.Mesh);
            Assert.Equal(2, Extras(light)["LightType"]!.GetValue<long>());
        }

        [Fact]
        public void ToGlb_WritesVerticesAsPoints_WhenCoronaHasNoTriangles()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header().TopLevel()
                .SizedString("DDCorona").NiObject(0x10, "Glow").NiAVObjectFields()
                .UInt16(1)
                .UInt32(PRESENT).Floats(0, 0, 1)                // vertices
                .UInt32(0).Floats(0, 0, 0, 0)
                .UInt16(0).UInt16(0)
                .UInt32(0).UInt32(0).UInt32(0)
                .Floats(1.5f)                                   // size
                .EndOfFile()
                .ToArray();

            // Act
            var corona = Assert.Single(SceneRoot(Convert(bytes)).VisualChildren);

            // Assert
            Assert.Equal(PrimitiveType.POINTS, Assert.Single(corona.Mesh.Primitives).DrawPrimitiveType);
            Assert.Equal(1.5f, Extras(corona)["Size"]!.GetValue<float>());
        }
    }
}
