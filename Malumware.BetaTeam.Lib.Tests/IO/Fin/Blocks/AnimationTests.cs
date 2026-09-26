using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Animation;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Blocks
{
    public class AnimationTests
    {
        // The animation settings shared by every 3ds animation class
        private static FinStreamBuilder Core(FinStreamBuilder builder)
        {
            return builder
                .UInt32(2)                          // animation type
                .Byte(1).Byte(2).Byte(3)            // unknown
                .Byte(1)                            // scene graph update
                .UInt32(1)                          // cycle type
                .Floats(2)                          // default display time
                .Floats(1)                          // frequency
                .Floats(0.25f)                      // phase
                .Floats(0.5f)                       // begin key time
                .Floats(3200);                      // end key time
        }

        // A NiTriShape with 2 vertices, no optional arrays and no triangles
        private static FinStreamBuilder TriShapeFields(FinStreamBuilder builder)
        {
            return builder.NiAVObjectFields()
                .UInt16(2)                          // vertex count
                .UInt32(0).UInt32(0)                // no vertices, no normals
                .Floats(0, 0, 0, 0)                 // bound
                .UInt16(0).UInt16(0)                // triangles, texture sets
                .UInt32(0).UInt32(0).UInt32(0);     // no texture coordinates, colours, planes
        }

        private static FinFile Read(FinStreamBuilder builder)
        {
            return FinReader.Read("TEST", builder.EndOfFile().ToArray());
        }

        [Fact]
        public void Read_ReadsRawData_WhenExtraDataHasNoClassName()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("NiNode")
                .UInt32(0x10).CString(null)         // link ID, name
                .UInt32(1)                          // extra data count
                .CString(null)                      // no class name: plain NiExtraData
                .UInt32(3).Byte(7).Byte(8).Byte(9)  // size and bytes
                .NiAVObjectFields().NiNodeFields([], []);

            // Act
            var node = Assert.IsType<NiNode>(Assert.Single(Read(builder).Objects));

            // Assert
            var extraData = Assert.Single(node.ExtraData);
            Assert.Equal("NiExtraData", extraData.ClassName);
            Assert.Equal(3u, extraData.Size);
            Assert.Equal([7, 8, 9], extraData.Data);
        }

        [Fact]
        public void Read_LinksFlipTextures_WhenTexturePropExtraDataIsValid()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("NiTextureProperty")
                .UInt32(0x10).CString(null)
                .UInt32(1).CString("TexturePropExtraData")
                .UInt32(4)                          // size, ignored for subclasses
                .UInt32(0x20)                       // flip textures
                .Int32(3)                           // index
                .Byte(0).Int32(0).Refs()            // master, index, images
                .SizedString("NiFlipTextures").NiObject(0x20).UInt32(0).Floats(1, 0, 1).UInt32(0x10);

            // Act
            var file = Read(builder);

            // Assert
            var texture = Assert.IsType<NiTextureProperty>(file.Objects[0]);
            var extraData = Assert.IsType<TexturePropExtraData>(Assert.Single(texture.ExtraData));
            Assert.Same(file.Objects[1], extraData.FlipTextures.Target);
            Assert.Equal(3, extraData.Index);
        }

        [Fact]
        public void Read_ReturnsAlphaAnimator_WithCoreKeysAndTarget()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .SizedString("NiMaterialProperty").UInt32(0x10).CString(null)
                .UInt32(1).CString("Ni3dsPropAnimExtraData").UInt32(4).UInt32(0x20)
                .Byte(0).Floats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
            builder = Core(builder.SizedString("Ni3dsAlphaAnimator").NiObject(0x20))
                .UInt32(0x10)                       // target
                .Int32(1)                           // key count
                .UInt32(3)                          // TCB
                .Floats(0.25f, 0.75f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f);

            // Act
            var file = Read(builder);

            // Assert
            var animator = Assert.IsType<Ni3dsAlphaAnimator>(file.Objects[1]);
            Assert.Equal(2u, animator.Core.AnimationType);
            Assert.True(animator.Core.SceneGraphUpdate);
            Assert.Equal(1u, animator.Core.CycleType);
            Assert.Equal(0.5f, animator.Core.BeginKeyTime);
            Assert.Equal(1f, animator.Core.Frequency);
            Assert.Equal(0.25f, animator.Core.Phase);
            Assert.Equal(3200f, animator.Core.EndKeyTime);
            Assert.Same(file.Objects[0], animator.Target.Target);
            Assert.Equal(3u, animator.Keys.KeyType);
            var key = Assert.IsType<FinTcbFloatKey>(Assert.Single(animator.Keys.Keys));
            Assert.Equal(new FinTcbFloatKey(0.25f, 0.75f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f), key);

            var material = Assert.IsType<NiMaterialProperty>(file.Objects[0]);
            var extraData = Assert.IsType<Ni3dsPropAnimExtraData>(Assert.Single(material.ExtraData));
            Assert.Same(animator, extraData.Animator.Target);
        }

        [Fact]
        public void Read_ReadsKeyTypeEvenWithoutKeys_WhenColorAnimatorIsEmpty()
        {
            // Arrange
            var builder = Core(new FinStreamBuilder().Header().SizedString("Ni3dsColorAnimator").NiObject(0x20))
                .UInt32(0)                          // no target
                .UInt32(7)                          // unknown
                .Int32(0)                           // key count
                .UInt32(1);                         // key type, read even for zero keys

            // Act
            var animator = Assert.IsType<Ni3dsColorAnimator>(Assert.Single(Read(builder).Objects));

            // Assert
            Assert.Equal(7u, animator.Unknown44);
            Assert.Empty(animator.Keys.Keys);
            Assert.True(animator.Target.IsNull);
        }

        [Fact]
        public void Read_ReadsColorKeysAsPositions_WhenColorAnimatorHasLinearKeys()
        {
            // Arrange
            var builder = Core(new FinStreamBuilder().Header().SizedString("Ni3dsColorAnimator").NiObject(0x20))
                .UInt32(0).UInt32(0)
                .Int32(1).UInt32(1)
                .Floats(0, 1, 0.5f, 0);             // time, colour

            // Act
            var animator = Assert.IsType<Ni3dsColorAnimator>(Assert.Single(Read(builder).Objects));

            // Assert
            Assert.Equal(new FinLinearPosKey(0, new Vector3(1, 0.5f, 0)), Assert.Single(animator.Keys.Keys));
        }

        [Fact]
        public void Read_ReturnsAnimationNode_WithEveryKeyList()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("Ni3dsAnimationNode").NiObject(0x10)
                .NiAVObjectFields().NiNodeFields([], []);
            builder = Core(builder)
                .Int32(1).UInt32(1)                                     // rotation: linear
                .Floats(0, 1.5f, 0, 0, 1, 1, 0, 0, 0).Int32(2).UInt32(9)
                .Int32(1).UInt32(2)                                     // position: bezier
                .Floats(1, 1, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
                .Int32(0)                                               // scale: no keys, so no type
                .Int32(2).Floats(0).Byte(1).Floats(1).Byte(0);          // visibility

            // Act
            var node = Assert.IsType<Ni3dsAnimationNode>(Assert.Single(Read(builder).Objects));

            // Assert
            var rotation = Assert.IsType<FinLinearRotKey>(Assert.Single(node.RotationKeys.Keys));
            Assert.Equal(1.5f, rotation.Angle);
            Assert.Equal(Vector3.UnitZ, rotation.Axis);
            Assert.Equal(new FinQuaternion(1, 0, 0, 0), rotation.Quaternion);
            Assert.Equal(2, rotation.ExtraSpins);
            var position = Assert.IsType<FinBezierPosKey>(Assert.Single(node.PositionKeys.Keys));
            Assert.Equal(new Vector3(1, 2, 3), position.Value);
            Assert.Empty(node.ScaleKeys.Keys);
            Assert.Equal([new FinVisKey(0, true), new FinVisKey(1, false)], node.VisibilityKeys);
        }

        [Fact]
        public void Read_ReadsNestedFloatKeys_WhenRotationKeyIsEuler()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("Ni3dsBone").NiObject(0x10)
                .NiAVObjectFields().NiNodeFields([], []);
            builder = Core(builder)
                .Int32(1).UInt32(4)                                     // rotation: euler
                .Floats(0, 0, 0, 0, 0, 1, 0, 0, 0).Int32(0).UInt32(0)
                .UInt16(5)                                              // unknown
                .Int32(1).UInt32(1).Floats(0, 0.5f)                     // x: one linear key
                .Int32(0)                                               // y: none
                .Int32(1).UInt32(2).Floats(1, 2, 3, 4)                  // z: one bezier key
                .Int32(0).Int32(0).Int32(0);                            // position, scale, visibility

            // Act
            var bone = Assert.IsType<Ni3dsBone>(Assert.Single(Read(builder).Objects));

            // Assert
            var euler = Assert.IsType<FinEulerRotKey>(Assert.Single(bone.RotationKeys.Keys));
            Assert.Equal(5, euler.Unknown3C);
            Assert.Equal(new FinLinearFloatKey(0, 0.5f), Assert.Single(euler.X.Keys));
            Assert.Empty(euler.Y.Keys);
            Assert.Equal(new FinBezierFloatKey(1, 2, 3, 4), Assert.Single(euler.Z.Keys));
        }

        [Fact]
        public void Read_ReadsTcbAndBezierRotationKeys()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("Ni3dsAnimationNode").NiObject(0x10)
                .NiAVObjectFields().NiNodeFields([], []);
            builder = Core(builder)
                .Int32(1).UInt32(3)                                     // rotation: TCB
                .Floats(0, 0, 0, 0, 0, 1, 0, 0, 0).Int32(0).UInt32(0)
                .Floats(0.1f, 0.2f, 0.3f)                               // tension, continuity, bias
                .Floats(1, 0, 0, 0, 0, 1, 0, 0)                         // two quaternions
                .Floats(8, 9)                                           // unknown
                .Int32(0).Int32(0).Int32(0);

            // Act
            var node = Assert.IsType<Ni3dsAnimationNode>(Assert.Single(Read(builder).Objects));

            // Assert
            var key = Assert.IsType<FinTcbRotKey>(Assert.Single(node.RotationKeys.Keys));
            Assert.Equal(0.2f, key.Continuity);
            Assert.Equal(new FinQuaternion(0, 1, 0, 0), key.QuaternionB);
            Assert.Equal(9f, key.Unknown60);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenKeyTypeIsUnknown()
        {
            // Arrange
            var builder = Core(new FinStreamBuilder().Header().SizedString("Ni3dsAlphaAnimator").NiObject(0x20))
                .UInt32(0).Int32(1).UInt32(7);

            // Act
            var act = () => Read(builder);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("key type 7", exception.Message);
        }

        [Fact]
        public void Read_LinksSkinInfluencesToBones()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("Ni3dsSkin").NiObject(0x10);
            builder = TriShapeFields(builder)
                .Byte(4)                                                // unknown
                .Byte(1)                                                // has skin data
                .UInt16(1).Floats(1, 0, 0, 0).UInt32(0x20)              // vertex 0: one influence
                .UInt16(2).Floats(0.5f, 1, 2, 3).UInt32(0x20).Floats(0.5f, 0, 0, 0).UInt32(0);
            builder = Core(builder.SizedString("Ni3dsBone").NiObject(0x20).NiAVObjectFields().NiNodeFields([], []))
                .Int32(0).Int32(0).Int32(0).Int32(0);

            // Act
            var file = Read(builder);

            // Assert
            var skin = Assert.IsType<Ni3dsSkin>(file.Objects[0]);
            Assert.Equal(4, skin.Unknown12);
            Assert.Equal(2, skin.SkinVertices!.Count);
            var influence = skin.SkinVertices[1][0];
            Assert.Equal(0.5f, influence.Weight);
            Assert.Equal(new Vector3(1, 2, 3), influence.Offset);
            Assert.Same(file.Objects[1], influence.Bone.Target);
            Assert.True(skin.SkinVertices[1][1].Bone.IsNull);
        }

        [Fact]
        public void Read_ReturnsMorphTargets_WhenMorphShapeIsValid()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("Ni3dsMorphShape").NiObject(0x10);
            builder = Core(TriShapeFields(builder))
                .Byte(1).Byte(0)                                        // unknown
                .Int32(2)                                               // target count
                .Int32(1)                                               // key count
                .Floats(0, 0, 0, 5)                                     // bound
                .UInt32(6)                                              // cubic morph keys
                .Floats(0, 1, 0, 0, 0, 0, 0, 7, 8)                      // TCB float key + two floats
                .Floats(1, 1, 1, 2, 2, 2)                               // target 0
                .Floats(3, 3, 3, 4, 4, 4);                              // target 1

            // Act
            var morph = Assert.IsType<Ni3dsMorphShape>(Assert.Single(Read(builder).Objects));

            // Assert
            Assert.Equal(5f, morph.MorphBoundRadius);
            var key = Assert.IsType<FinCubicMorphKey>(Assert.Single(morph.Keys.Keys));
            Assert.Equal(8f, key.Unknown24);
            Assert.Equal(2, morph.Targets.Count);
            Assert.Equal(new Vector3(4, 4, 4), morph.Targets[1][1]);
        }

        [Fact]
        public void Read_ThrowsInvalidDataException_WhenAbstractMorphKeysHaveCount()
        {
            // Arrange: type 4 keys take no bytes, so an unchecked count would only cost memory
            var builder = new FinStreamBuilder().Header().SizedString("Ni3dsMorphShape").NiObject(0x10);
            builder = Core(TriShapeFields(builder))
                .Byte(0).Byte(0)
                .Int32(0)                                               // target count
                .Int32(2)                                               // key count
                .Floats(0, 0, 0, 0)
                .UInt32(4);                                             // NiMorphKey: the engine creates nothing

            // Act
            var act = () => Read(builder);

            // Assert
            var exception = Assert.Throws<InvalidDataException>(act);
            Assert.Contains("key type 4", exception.Message);
        }

        [Fact]
        public void Read_ReturnsEmptyKeys_WhenAbstractMorphKeysHaveNoCount()
        {
            // Arrange
            var builder = new FinStreamBuilder().Header().SizedString("Ni3dsMorphShape").NiObject(0x10);
            builder = Core(TriShapeFields(builder))
                .Byte(0).Byte(0)
                .Int32(0)                                               // target count
                .Int32(0)                                               // key count
                .Floats(0, 0, 0, 0)
                .UInt32(4);

            // Act
            var morph = Assert.IsType<Ni3dsMorphShape>(Assert.Single(Read(builder).Objects));

            // Assert
            Assert.Equal(4u, morph.Keys.KeyType);
            Assert.Empty(morph.Keys.Keys);
        }
    }
}
