using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Animation;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using static Malumware.BetaTeam.Lib.Tests.IO.Fin.Animation.AnimatedFin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Animation
{
    public class FinSkillPlanTests
    {
        private static readonly Track MOVES = new(Positions: [(0, Vector3.Zero), (4800, Vector3.UnitX)]);
        private static readonly Track STILL = new();

        // Actor 0x01 > A (0x10) > B (0x11), and C (0x12) next to A; shared data 0x02
        private static FinFile ThreeNodes(Skill[] skills, Track[] tracks)
        {
            var builder = Start()
                .Actor(0x01, 0x02, 0x10, 0x12)
                .AnimNode(0x10, "A", [0x11])
                .AnimNode(0x11, "B", [])
                .AnimNode(0x12, "C", [])
                .SharedData(0x02, skills, tracks);

            return Read(builder);
        }

        private static IReadOnlyList<string?> NodeNames(FinSkillPlan plan)
        {
            return plan.Nodes.Select(node => node.Node.Name).ToList();
        }

        [Fact]
        public void AnimationNodes_FollowPreOrder()
        {
            var file = ThreeNodes([], [MOVES, STILL, new Track(Scales: [(0, 2)])]);
            var actor = file.Objects.OfType<DDActor>().Single();

            var nodes = FinSkillPlan.AnimationNodes(actor, actor.SharedData.Target!);

            Assert.Equal(["A", "B", "C"], nodes.Select(node => node.Node.Name));
            Assert.Same(actor.SharedData.Target!.Tracks[2], nodes[2].Track);
        }

        [Fact]
        public void AnimationNodes_NodeListedTwice_KeepsTheLastTrack()
        {
            // The engine walks A once per place it's listed and replaces its keys each time
            var builder = Start()
                .Actor(0x01, 0x02, 0x20, 0x20)
                .Node(0x20, "P", 0x10)
                .AnimNode(0x10, "A", [])
                .SharedData(0x02, [], [MOVES, STILL]);
            var file = Read(builder);
            var actor = file.Objects.OfType<DDActor>().Single();

            var nodes = FinSkillPlan.AnimationNodes(actor, actor.SharedData.Target!);

            Assert.Equal(2, nodes.Count);
            Assert.All(nodes, node => Assert.Same(actor.SharedData.Target!.Tracks[1], node.Track));
        }

        [Fact]
        public void AnimationNodes_MoreNodesThanTracks_Throws()
        {
            var file = ThreeNodes([], [MOVES, STILL]);
            var actor = file.Objects.OfType<DDActor>().Single();

            Assert.Throws<InvalidDataException>(() => FinSkillPlan.AnimationNodes(actor, actor.SharedData.Target!));
        }

        [Fact]
        public void SkillNodes_IncludesTrackWithKeyInRange()
        {
            var file = ThreeNodes([new Skill("walk", 0.5f, 2)],
                [STILL, new Track(Positions: [(0, Vector3.Zero), (4800, Vector3.UnitX), (9600, Vector3.Zero)]), STILL]);

            var plan = Assert.Single(FinSkillPlan.Build(file));

            Assert.Equal(["B"], NodeNames(plan));
            Assert.Equal(2400, plan.StartTicks);
            Assert.Equal(9600, plan.EndTicks);
        }

        [Fact]
        public void SkillNodes_ExcludesTrackWithKeysOnlyOutside()
        {
            var outside = new Track(Positions: [(0, Vector3.UnitX), (100, Vector3.UnitX), (9600, Vector3.UnitX)]);
            var file = ThreeNodes([new Skill("walk", 0.5f, 1)], [outside, STILL, STILL]);

            Assert.Empty(Assert.Single(FinSkillPlan.Build(file)).Nodes);
        }

        [Fact]
        public void SkillNodes_IncludesTrackWhosePoseDiffersAtStart()
        {
            var turns = new Track(Rotations: [(0, Quaternion.Identity),
                (1000, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1))]);
            var file = ThreeNodes([new Skill("turn", 400 / 4800f, 600 / 4800f)], [STILL, STILL, turns]);

            Assert.Equal(["C"], NodeNames(Assert.Single(FinSkillPlan.Build(file))));
        }

        [Fact]
        public void SkillNodes_VisibilityChangeCounts()
        {
            var hides = new Track(Visibility: [(0, true), (1000, false)]);
            var file = ThreeNodes([new Skill("hide", 2000 / 4800f, 3000 / 4800f)], [hides, STILL, STILL]);

            Assert.Equal(["A"], NodeNames(Assert.Single(FinSkillPlan.Build(file))));
        }

        [Fact]
        public void SkillNodes_KeepsTheFileIndices()
        {
            var file = ThreeNodes([new Skill("walk", 0, 1, Nodes: [2])], [MOVES, STILL, STILL]);

            Assert.Equal(["C", "A"], NodeNames(Assert.Single(FinSkillPlan.Build(file))));
        }

        [Fact]
        public void Build_NodesListedTwiceAtEveryLevel_ThrowsInsteadOfWalkingForever()
        {
            // Each node lists the next one twice, so a walk would visit 2^40 places
            const uint FIRST = 0x100;
            const int LEVELS = 40;
            var builder = Start().Actor(0x01, 0x02, FIRST);
            for (uint i = 0; i < LEVELS; i++)
            {
                builder = i == LEVELS - 1
                    ? builder.Node(FIRST + i, $"N{i}")
                    : builder.Node(FIRST + i, $"N{i}", FIRST + i + 1, FIRST + i + 1);
            }
            builder = builder.SharedData(0x02, [new Skill("walk", 0, 1)], []);

            Assert.Throws<InvalidDataException>(() => FinSkillPlan.Build(Read(builder)));
        }

        [Theory]
        [InlineData(0, float.PositiveInfinity)]
        [InlineData(float.NaN, 1)]
        [InlineData(0, 1e30f)]
        [InlineData(0, 3601)]
        public void Build_UnusableSkillTimes_Throw(float start, float end)
        {
            var file = ThreeNodes([new Skill("broken", start, end)], [MOVES, STILL, STILL]);

            Assert.Throws<InvalidDataException>(() => FinSkillPlan.Build(file));
        }

        [Fact]
        public void Build_WithoutActor_IsEmpty()
        {
            var file = Read(Start().TopLevel().Node(0x10, "Root"));

            Assert.Empty(FinSkillPlan.Build(file));
        }

        [Fact]
        public void FlipBooks_AreFoundByTheirExtraDataIndex()
        {
            var builder = Start()
                .Actor(0x01, 0x02, 0x10)
                .Shape(0x10, "Screen", 0x20, 0x30)
                .FlipBook(0x20, 0x21, 7, 0, 0, 400, 0x40, 0x41)
                .FlipBook(0x30, 0x31, 3, 0, 0, 400, 0x40, 0x41)
                .Image(0x40, "a.tga")
                .Image(0x41, "b.tga")
                .SharedData(0x02, [new Skill("blink", 0, 1, Actions: [3, 9])], []);

            var plan = Assert.Single(FinSkillPlan.Build(Read(builder)));

            var use = Assert.Single(plan.FlipBooks);
            Assert.Equal(0x31u, use.FlipBook.LinkId);
            Assert.Equal(0x30u, use.Textures.LinkId);
        }

        [Fact]
        public void Morphs_AreFoundByTheirIndexInTreeOrder()
        {
            Vector3[] triangle = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
            var builder = Start()
                .Actor(0x01, 0x02, 0x10, 0x11)
                .MorphShape(0x10, "First", [triangle, triangle], (0, [0f, 1f]))
                .MorphShape(0x11, "Second", [triangle, triangle], (0, [0f, 1f]))
                .SharedData(0x02, [new Skill("wave", 0, 1, Others: [1])], []);

            var plan = Assert.Single(FinSkillPlan.Build(Read(builder)));

            Assert.Equal("Second", Assert.Single(plan.Morphs).Name);
        }

        [Fact]
        public void PropertyAnimators_KeepDiffuseEmissiveAndAlpha()
        {
            var builder = Start()
                .Material(0x10, Vector3.One, 1, 0x20, 0x21, 0x22)
                .ColorAnimator(0x20, 0x10, 1, (0, Vector3.One))
                .ColorAnimator(0x21, 0x10, 3, (0, Vector3.One))
                .AlphaAnimator(0x22, 0x10, (0, 1f));

            var animation = Assert.Single(FinSkillPlan.PropertyAnimators(Read(builder)));

            Assert.Equal(0x10u, animation.Material.LinkId);
            Assert.Equal(0x20u, animation.Diffuse!.LinkId);
            Assert.Equal(0x21u, animation.Emissive!.LinkId);
            Assert.Equal(0x22u, animation.Alpha!.LinkId);
        }

        [Fact]
        public void PropertyAnimators_SkipAmbientAndSpecular()
        {
            var builder = Start()
                .Material(0x10, Vector3.One, 1, 0x20, 0x21)
                .ColorAnimator(0x20, 0x10, 0, (0, Vector3.One))
                .ColorAnimator(0x21, 0x10, 2, (0, Vector3.One));

            Assert.Empty(FinSkillPlan.PropertyAnimators(Read(builder)));
        }
    }
}
