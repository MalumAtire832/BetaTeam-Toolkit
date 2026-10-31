using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin;
using Malumware.BetaTeam.Lib.IO.Fin.Animation;
using Malumware.BetaTeam.Lib.IO.Fin.Gltf;
using SharpGLTF.Schema2;
using GltfAnimation = SharpGLTF.Schema2.Animation;
using static Malumware.BetaTeam.Lib.Tests.IO.Fin.Animation.AnimatedFin;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    public class FinGltfAnimationsTests
    {
        private const float TOLERANCE = 1e-5f;
        private static readonly Track STILL = new();

        private static ModelRoot Convert(FinStreamBuilder builder, bool includeAnimations = true)
        {
            var file = Read(builder);

            return GlbTestReader.Read(new FinToGltfConverter(includeAnimations: includeAnimations).ToGlb(file));
        }

        // Actor 0x01 > A (0x10), shared data 0x02
        private static FinStreamBuilder OneNode(Skill[] skills, Track track, float[]? rotation = null, float scale = 1)
        {
            return Start()
                .Actor(0x01, 0x02, 0x10)
                .AnimNode(0x10, "A", [], rotation, scale)
                .SharedData(0x02, skills, [track]);
        }

        private static Node NodeNamed(ModelRoot model, string name)
        {
            return model.LogicalNodes.Single(node => node.Name == name);
        }

        private static IReadOnlyList<(float Key, Vector3 Value)> Translations(GltfAnimation animation, Node node)
        {
            return animation.FindTranslationChannel(node).GetTranslationSampler().GetLinearKeys().ToList();
        }

        [Fact]
        public void EverySkillBecomesAnAnimation()
        {
            var moves = new Track(Positions: [(0, Vector3.Zero), (4800, Vector3.UnitX), (9600, Vector3.Zero)]);

            var model = Convert(OneNode([new Skill("walk", 0, 1), new Skill("back", 1, 2)], moves));

            Assert.Equal(["walk", "back"], model.LogicalAnimations.Select(animation => animation.Name));
        }

        [Fact]
        public void Translation_IsSampledAt60HzPlusKeyTimes()
        {
            var moves = new Track(Positions: [(0, Vector3.Zero), (250, Vector3.UnitX), (1000, Vector3.UnitY)]);

            var model = Convert(OneNode([new Skill("walk", 0, 0.1f)], moves));

            var times = Translations(model.LogicalAnimations[0], NodeNamed(model, "A")).Select(key => key.Key).ToList();
            float[] expected = [0, 80 / 4800f, 160 / 4800f, 240 / 4800f, 250 / 4800f, 320 / 4800f, 400 / 4800f, 480 / 4800f];
            Assert.Equal(expected.Length, times.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], times[i], TOLERANCE);
            }
        }

        [Fact]
        public void Translation_ValuesMatchTheEvaluator()
        {
            var moves = new Track(Positions: [(0, Vector3.Zero), (250, new Vector3(10, 20, 30)), (1000, Vector3.UnitY)]);
            var file = Read(OneNode([new Skill("walk", 0.02f, 0.1f)], moves));
            var keys = file.Objects.OfType<Malumware.BetaTeam.Lib.IO.Fin.Blocks.DDActorSharedData>().Single().Tracks[0].PositionKeys;

            var model = GlbTestReader.Read(new FinToGltfConverter().ToGlb(file));

            foreach (var (time, value) in Translations(model.LogicalAnimations[0], NodeNamed(model, "A")))
            {
                var expected = FinKeyEvaluator.Position(keys, FinAnimationTime.ToTicks(0.02f) + time * 4800);
                Assert.Equal(expected.X, value.X, 1e-3f);
                Assert.Equal(expected.Y, value.Y, 1e-3f);
                Assert.Equal(expected.Z, value.Z, 1e-3f);
            }
        }

        [Fact]
        public void RotationChannel_ComesWithUniformScale()
        {
            var turns = new Track(Rotations: [(0, Quaternion.Identity), (4800, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1))]);
            // A mirrored rest rotation, which the glb writes as a negative scale
            var model = Convert(OneNode([new Skill("turn", 0, 1)], turns, [1, 0, 0, 0, 1, 0, 0, 0, -1], 2));

            var animation = model.LogicalAnimations[0];
            var node = NodeNamed(model, "A");
            Assert.NotNull(animation.FindRotationChannel(node));
            var scales = animation.FindScaleChannel(node).GetScaleSampler().GetLinearKeys().ToList();
            Assert.NotEmpty(scales);
            Assert.All(scales, key => Assert.Equal(new Vector3(2, 2, 2), key.Value));
        }

        [Fact]
        public void Rotation_ValuesAreUnitQuaternions()
        {
            var turns = new Track(Rotations: [(0, Quaternion.Identity), (4800, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 1))]);

            var model = Convert(OneNode([new Skill("turn", 0, 1)], turns));

            var keys = model.LogicalAnimations[0].FindRotationChannel(NodeNamed(model, "A")).GetRotationSampler().GetLinearKeys();
            var last = keys.Last().Value;
            Assert.Equal(1, last.Length(), TOLERANCE);
            Assert.Equal(MathF.Sin(0.5f), MathF.Abs(last.Z), TOLERANCE);
        }

        [Fact]
        public void NodeWithoutDataInSkill_HasNoChannel()
        {
            var builder = Start()
                .Actor(0x01, 0x02, 0x10, 0x11)
                .AnimNode(0x10, "A", [])
                .AnimNode(0x11, "B", [])
                .SharedData(0x02, [new Skill("walk", 0, 1)],
                    [new Track(Positions: [(0, Vector3.Zero), (4800, Vector3.UnitX)]), STILL]);

            var model = Convert(builder);

            var animation = model.LogicalAnimations[0];
            Assert.NotNull(animation.FindTranslationChannel(NodeNamed(model, "A")));
            Assert.Empty(animation.FindChannels(NodeNamed(model, "B")));
        }

        [Fact]
        public void DuplicatedNode_IsAnimatedInEveryCopy()
        {
            // P is listed twice, so the engine walks A twice and A ends up with the second track
            var builder = Start()
                .Actor(0x01, 0x02, 0x20, 0x20)
                .Node(0x20, "P", 0x10)
                .AnimNode(0x10, "A", [])
                .SharedData(0x02, [new Skill("walk", 0, 1)],
                    [new Track(Positions: [(0, Vector3.Zero), (4800, Vector3.UnitX)]),
                        new Track(Positions: [(0, Vector3.Zero), (4800, Vector3.UnitY)])]);

            var model = Convert(builder);

            var copies = model.LogicalNodes.Where(node => node.Name == "A").ToList();
            Assert.Equal(2, copies.Count);
            foreach (var copy in copies)
            {
                Assert.Single(model.LogicalAnimations[0].FindChannels(copy));
                Assert.Equal(Vector3.UnitY, Translations(model.LogicalAnimations[0], copy).Last().Value);
            }
        }

        [Fact]
        public void ZeroLengthSkill_HasOneKeyAtZero()
        {
            var moves = new Track(Positions: [(0, Vector3.Zero), (4800, Vector3.UnitX)]);

            var model = Convert(OneNode([new Skill("pose", 0.5f, 0.5f)], moves));

            var key = Assert.Single(Translations(model.LogicalAnimations[0], NodeNamed(model, "A")));
            Assert.Equal(0, key.Key);
            Assert.Equal(0.5f, key.Value.X, TOLERANCE);
        }

        [Fact]
        public void SkillWithNothingToAnimate_WritesNoAnimation()
        {
            var model = Convert(OneNode([new Skill("neutral", 0, 1)], STILL));

            Assert.Empty(model.LogicalAnimations);
        }

        [Fact]
        public void AnimatedNodeWithMatrixTransform_Throws()
        {
            var moves = new Track(Positions: [(0, Vector3.Zero), (4800, Vector3.UnitX)]);
            var file = Read(OneNode([new Skill("walk", 0, 1)], moves, [2, 0, 0, 0, 1, 0, 0, 0, 1]));

            var error = Assert.Throws<InvalidDataException>(() => new FinToGltfConverter().ToGlb(file));
            Assert.Contains("\"A\"", error.Message);
        }

        [Fact]
        public void MorphShape_WritesTargetsAndWeights()
        {
            Vector3[] rest = [Vector3.Zero, Vector3.UnitX, Vector3.UnitY];
            Vector3[] up = [Vector3.UnitZ, Vector3.UnitX + Vector3.UnitZ, Vector3.UnitY + Vector3.UnitZ];
            Vector3[] wide = [Vector3.Zero, 2 * Vector3.UnitX, 2 * Vector3.UnitY];
            var builder = Start()
                .Actor(0x01, 0x02, 0x10)
                .MorphShape(0x10, "Wave", [rest, up, wide], (0, [1f, 0f, 0f]), (4800, [0f, 1f, 0.5f]))
                .SharedData(0x02, [new Skill("wave", 0, 1, Others: [0])], []);

            var model = Convert(builder);

            var primitive = model.LogicalMeshes.Single().Primitives.Single();
            Assert.Equal(2, primitive.MorphTargetsCount);
            var delta = primitive.GetMorphTargetAccessors(0)["POSITION"].AsVector3Array();
            Assert.Equal(Vector3.UnitZ, delta[1]);
            var weights = model.LogicalAnimations[0].FindMorphChannel(NodeNamed(model, "Wave")).GetMorphSampler()
                .GetLinearKeys().ToList();
            Assert.Equal([0f, 0f], weights[0].Value);
            Assert.Equal(1, weights[^1].Value[0], TOLERANCE);
            Assert.Equal(0.5f, weights[^1].Value[1], TOLERANCE);
        }

        [Fact]
        public void Converter_WithoutAnimations_WritesNone()
        {
            var moves = new Track(Positions: [(0, Vector3.Zero), (4800, Vector3.UnitX)]);

            var model = Convert(OneNode([new Skill("walk", 0, 1)], moves), includeAnimations: false);

            Assert.Empty(model.LogicalAnimations);
        }
    }
}
