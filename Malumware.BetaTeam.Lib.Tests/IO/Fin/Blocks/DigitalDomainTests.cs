using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Animation;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Blocks
{
    public class DigitalDomainTests
    {
        private static FinStreamBuilder SharedData(FinStreamBuilder builder, uint linkId)
        {
            return builder.SizedString("DDActorSharedData").NiObject(linkId, "U0001")
                .CString("Test unit")                   // description
                .CString(null)                          // unknown
                .Byte(1)                                // make shadow
                .Byte(0)                                // prop
                .Floats(0.5f)                           // shadow multiplier
                .UInt32(9)                              // unknown
                .Int32(2)                               // skills
                .CString("neutral").Floats(0, 1)        // name, start, end
                .Byte(0)                                // no sound
                .Int32(2).UInt16(0).UInt16(3)           // animation nodes
                .Int32(0)                               // actions
                .Int32(1).UInt16(1)                     // other animations
                .CString("drive").Floats(1, 3)
                .Byte(1)                                // sound follows
                .CString("engine.dds").CString("")
                .Byte(1).Byte(0).Byte(1)                // loop, source type, distance flag
                .Floats(0.25f, 0.8f, 2, 100, 5)         // delay, gain, distance scale, max, min distance
                .Int32(0).Int32(1).UInt16(4).Int32(0)
                .Int32(1)                               // tracks
                .Int32(0)                               // rotation keys
                .Int32(1).UInt32(1).Floats(0, 1, 2, 3)  // position keys: one linear
                .Int32(0)                               // scale keys
                .Int32(1).Floats(0).Byte(1)             // visibility keys
                .UInt32(2).Floats(1, 0, 0, 0, 1, 0);    // floor points
        }

        [Fact]
        public void Read_ReturnsSharedData_WithSkillsTracksAndFloorPoints()
        {
            // Arrange
            var bytes = SharedData(new FinStreamBuilder().Header(), 0x10).EndOfFile().ToArray();

            // Act
            var shared = Assert.IsType<DDActorSharedData>(Assert.Single(FinReader.Read("TEST", bytes).Objects));

            // Assert
            Assert.Equal("U0001", shared.Name);
            Assert.Equal("Test unit", shared.Description);
            Assert.Null(shared.Unknown14);
            Assert.True(shared.MakeShadow);
            Assert.False(shared.IsProp);
            Assert.Equal(0.5f, shared.ShadowMultiplier);
            Assert.Equal(9u, shared.Unknown74);

            Assert.Equal(2, shared.Skills.Count);
            var neutral = shared.Skills[0];
            Assert.Equal("neutral", neutral.Name);
            Assert.Equal(1f, neutral.EndTime);
            Assert.Null(neutral.Sound);
            Assert.Equal([0, 3], neutral.AnimationNodes);
            Assert.Empty(neutral.Actions);
            Assert.Equal([1], neutral.OtherAnimations);

            var sound = shared.Skills[1].Sound!;
            Assert.Equal("engine.dds", sound.FileName);
            Assert.Null(sound.NodeName);
            Assert.True(sound.Loop);
            Assert.Equal(0.25f, sound.Delay);
            Assert.Equal(0.8f, sound.Gain);
            Assert.Equal(100f, sound.MaxDistance);
            Assert.Equal(5f, sound.MinDistance);
            Assert.Equal([4], shared.Skills[1].Actions);

            var track = Assert.Single(shared.Tracks);
            Assert.Empty(track.RotationKeys.Keys);
            Assert.Equal(new FinLinearPosKey(0, new Vector3(1, 2, 3)), Assert.Single(track.PositionKeys.Keys));
            Assert.Equal([new FinVisKey(0, true)], track.VisibilityKeys);

            Assert.Equal([Vector3.UnitX, Vector3.UnitY], shared.FloorPoints);
        }

        [Theory]
        [InlineData("DDUnit")]
        [InlineData("DDEnv")]
        public void Read_LinksActorToSharedData(string className)
        {
            // Arrange
            var builder = new FinStreamBuilder().Header()
                .TopLevel().SizedString(className).NiObject(0x20, "Instance").NiAVObjectFields().NiNodeFields([], [])
                .UInt32(0x10);                          // shared data
            var bytes = SharedData(builder, 0x10).EndOfFile().ToArray();

            // Act
            var file = FinReader.Read("TEST", bytes);

            // Assert
            var actor = Assert.IsAssignableFrom<DDActor>(file.TopLevelObjects[0]);
            Assert.Equal(className, actor.GetType().Name);
            Assert.Same(file.Objects[1], actor.SharedData.Target);
        }

        [Fact]
        public void Read_ReturnsCoronaSize_WhenCoronaIsValid()
        {
            // Arrange
            var bytes = new FinStreamBuilder().Header().SizedString("DDCorona").NiObject(0x10, "DDCorona01")
                .NiAVObjectFields()
                .UInt16(4)                              // vertex count
                .UInt32(0).UInt32(0).Floats(0, 0, 0, 0)
                .UInt16(2).UInt16(0)                    // triangle count, texture sets
                .UInt32(0).UInt32(0).UInt32(0)
                .Floats(1.5f)                           // size; no triangle list follows
                .EndOfFile()
                .ToArray();

            // Act
            var corona = Assert.IsType<DDCorona>(Assert.Single(FinReader.Read("TEST", bytes).Objects));

            // Assert
            Assert.Equal(1.5f, corona.Size);
            Assert.Equal(2, corona.TriangleCount);
        }
    }
}
