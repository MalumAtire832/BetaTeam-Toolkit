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
        public void Visibility_IsAStepPointerOnTheNode()
        {
            var blinks = new Track(Visibility: [(0, true), (2400, false), (4000, true)]);

            var glb = new FinToGltfConverter().ToGlb(Read(OneNode([new Skill("blink", 0, 1)], blinks)));

            var model = GlbTestReader.Read(glb);
            var node = NodeNamed(model, "A");
            var channel = Assert.Single(model.LogicalAnimations[0].Channels);
            Assert.Equal($"/nodes/{node.LogicalIndex}/extensions/KHR_node_visibility/visible", channel.TargetPointerPath);
            var json = GlbTestReader.Json(glb);
            var sampler = json["animations"]![0]!["samplers"]![0]!;
            Assert.Equal("STEP", sampler["interpolation"]!.GetValue<string>());
            var visibility = node.GetCurveSamplers(model.LogicalAnimations[0]);
            Assert.True(visibility.GetVisibility(0));
            Assert.False(visibility.GetVisibility(2400 / 4800f));
            Assert.True(visibility.GetVisibility(4000 / 4800f));
        }

        // A lit triangle 0x10 with material 0x20 and the given animators
        private static FinStreamBuilder AnimatedMaterial(Skill[] skills, Func<FinStreamBuilder, FinStreamBuilder> animators,
            bool unlit = false, params uint[] animatorIds)
        {
            var builder = Start().Actor(0x01, 0x02, 0x10);
            builder = unlit ? builder.UnlitShape(0x10, "S", 0x20) : builder.Shape(0x10, "S", 0x20);
            builder = builder.Material(0x20, new Vector3(0.5f, 0.5f, 0.5f), 0.75f, animatorIds);

            return animators(builder).SharedData(0x02, skills, []);
        }

        private static IReadOnlyList<(float Key, T Value)> MaterialKeys<T>(ModelRoot model, string pointerEnd, int animation = 0)
        {
            var channel = model.LogicalAnimations[animation].Channels.Single(c => c.TargetPointerPath?.EndsWith(pointerEnd) == true);

            return channel.GetSamplerOrNull<T>().GetLinearKeys().ToList();
        }

        [Fact]
        public void DiffuseAnimator_AnimatesBaseColorFactorInLinear()
        {
            var builder = AnimatedMaterial([new Skill("glow", 0, 1)],
                b => b.ColorAnimator(0x30, 0x20, 1, (0, new Vector3(0.5f, 0.5f, 0.5f)), (4800, new Vector3(1.5f, 0, 0))),
                false, 0x30);

            var model = Convert(builder);

            var keys = MaterialKeys<Vector4>(model, "/pbrMetallicRoughness/baseColorFactor");
            Assert.Equal(0.21404114f, keys[0].Value.X, TOLERANCE);
            Assert.Equal(0.75f, keys[0].Value.W, TOLERANCE);
            Assert.Equal(new Vector4(1, 0, 0, 0.75f), keys[^1].Value);
        }

        [Fact]
        public void AlphaAnimator_AnimatesTheFourthComponent()
        {
            var builder = AnimatedMaterial([new Skill("fade", 0, 1)],
                b => b.AlphaAnimator(0x30, 0x20, (0, 1f), (4800, 0f)), false, 0x30);

            var model = Convert(builder);

            var keys = MaterialKeys<Vector4>(model, "/pbrMetallicRoughness/baseColorFactor");
            Assert.Equal(1, keys[0].Value.W, TOLERANCE);
            Assert.Equal(0, keys[^1].Value.W, TOLERANCE);
            Assert.Equal(0.21404114f, keys[^1].Value.X, TOLERANCE);
        }

        [Fact]
        public void EmissiveAnimator_AnimatesEmissiveFactor()
        {
            var builder = AnimatedMaterial([new Skill("glow", 0, 1)],
                b => b.ColorAnimator(0x30, 0x20, 3, (0, Vector3.Zero), (4800, Vector3.One)), false, 0x30);

            var model = Convert(builder);

            var keys = MaterialKeys<Vector3>(model, "/emissiveFactor");
            Assert.Equal(Vector3.Zero, keys[0].Value);
            Assert.Equal(Vector3.One, keys[^1].Value);
        }

        [Fact]
        public void UnlitMaterial_KeepsWhiteBaseColour()
        {
            var builder = AnimatedMaterial([new Skill("fade", 0, 1)],
                b => b
                    .ColorAnimator(0x30, 0x20, 1, (0, Vector3.Zero), (4800, Vector3.One))
                    .AlphaAnimator(0x31, 0x20, (0, 1f), (4800, 0f)),
                true, 0x30, 0x31);

            var model = Convert(builder);

            Assert.All(MaterialKeys<Vector4>(model, "/pbrMetallicRoughness/baseColorFactor"),
                key => Assert.Equal(new Vector3(1, 1, 1), new Vector3(key.Value.X, key.Value.Y, key.Value.Z)));
        }

        [Fact]
        public void AmbientAnimator_IsSkipped()
        {
            var builder = AnimatedMaterial([new Skill("glow", 0, 1)],
                b => b.ColorAnimator(0x30, 0x20, 0, (0, Vector3.Zero), (4800, Vector3.One)), false, 0x30);

            Assert.Empty(Convert(builder).LogicalAnimations);
        }

        [Fact]
        public void ColourAnimationsPlayInEverySkill()
        {
            var builder = AnimatedMaterial([new Skill("a", 0, 0.5f), new Skill("b", 0.5f, 1)],
                b => b.AlphaAnimator(0x30, 0x20, (0, 1f), (4800, 0f)), false, 0x30);

            var model = Convert(builder);

            Assert.Equal(["a", "b"], model.LogicalAnimations.Select(a => a.Name));
            Assert.Equal(0.5f, MaterialKeys<Vector4>(model, "baseColorFactor", 1)[0].Value.W, TOLERANCE);
        }

        [Fact]
        public void FlipBook_IsWrittenToTheRootExtras()
        {
            var builder = Start()
                .Actor(0x01, 0x02, 0x10)
                .Shape(0x10, "Screen", 0x20)
                .FlipBook(0x20, 0x21, 3, 0, 0, 400, 0x40, 0x41)
                .Image(0x40, "On.tga")
                .Image(0x41, "Off.tga")
                .SharedData(0x02, [new Skill("blink", 0, 500 / 4800f, Actions: [3])], []);

            var glb = new FinToGltfConverter().ToGlb(Read(builder));

            var json = GlbTestReader.Json(glb);
            var root = json["nodes"]!.AsArray().First(node => node!["extras"]?["file"] is not null)!;
            var flipbook = Assert.Single(root["extras"]!["flipbooks"]!.AsArray())!;
            Assert.Equal("blink", flipbook["skill"]!.GetValue<string>());
            Assert.Equal(0, flipbook["material"]!.GetValue<int>());
            var uris = GlbTestReader.ImageUris(glb);
            var images = flipbook["images"]!.AsArray().Select(i => uris[i!.GetValue<int>()]).ToList();
            Assert.Equal(["../textures/on.png", "../textures/off.png"], images);
            Assert.Equal([0, 1, 0], flipbook["frames"]!.AsArray().Select(f => f!.GetValue<int>()));
            float[] expected = [0, 200 / 4800f, 400 / 4800f];
            Assert.Equal(expected, flipbook["times"]!.AsArray().Select(t => t!.GetValue<float>()));
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
