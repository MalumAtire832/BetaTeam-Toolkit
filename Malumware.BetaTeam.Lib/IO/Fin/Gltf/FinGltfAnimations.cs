using System.Numerics;
using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO.Fin.Animation;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using SharpGLTF.Schema2;
using GltfAnimation = SharpGLTF.Schema2.Animation;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    // Cuts the object's single animation timeline into one glTF animation per skill. Every animated value is
    // evaluated the way the engine does (FinKeyEvaluator) at 60 samples a second plus every key inside the skill,
    // written as linear keys, so a player interpolating linearly stays close to the original between samples and
    // matches it at the keys. Times in the glb are seconds from the skill's start.
    internal static class FinGltfAnimations
    {
        // 60 samples a second is one every 80 ticks
        private const float SAMPLE_TICKS = FinAnimationTime.TICKS_PER_SECOND / 60;

        private const string BASE_COLOR = "pbrMetallicRoughness/baseColorFactor";
        private const string EMISSIVE = "emissiveFactor";

        // Writes the animations and returns the skills' flip-books, which have no glTF channel: an entry per skill and
        // material, naming the frame images and when each shows (the root node's extras hold them, because a skill
        // that only flips textures has no channel to make a glTF animation from)
        public static JsonArray Write(ModelRoot model, FinFile file, Func<NiAVObject, IReadOnlyList<Node>> nodesOf,
            FinGltfMaterials materials)
        {
            var plans = FinSkillPlan.Build(file);
            var materialAnimations = FinSkillPlan.PropertyAnimators(file);
            var flipBooks = new JsonArray();
            for (var i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                var name = plan.Skill.Name ?? $"skill_{i}";
                var channels = new List<Action<GltfAnimation>>();
                AddNodeChannels(plan, nodesOf, channels);
                AddMorphChannels(plan, nodesOf, channels);
                AddMaterialChannels(plan, materialAnimations, materials, channels);
                AddFlipBooks(plan, name, materials, flipBooks);
                if (channels.Count == 0)
                {
                    // glTF has no empty animations; the importer makes one for a skill without any
                    continue;
                }

                var animation = model.CreateAnimation(name);
                foreach (var channel in channels)
                {
                    channel(animation);
                }
            }

            return flipBooks;
        }

        // The weights a morph shape shows before any skill plays: its keys at time 0, for targets 1 and up
        public static float[] RestMorphWeights(Ni3dsMorphShape morph, int targetCount)
        {
            if (!morph.Keys.Keys.Any(key => key is FinBaryMorphKey))
            {
                return new float[targetCount - 1];
            }

            return MorphWeights(morph, targetCount, FinAnimationTime.ScaledTime(morph.Core, 0));
        }

        private static void AddNodeChannels(FinSkillPlan plan, Func<NiAVObject, IReadOnlyList<Node>> nodesOf,
            List<Action<GltfAnimation>> channels)
        {
            // A node reached twice has the same track both times; one set of channels per glTF copy is enough
            foreach (var animated in plan.Nodes.DistinctBy(e => e.Node, ReferenceEqualityComparer.Instance))
            {
                var nodes = nodesOf(animated.Node);
                if (nodes.Count == 0)
                {
                    continue;
                }

                var track = animated.Track;
                var core = animated.Node.Core;
                var hasRotation = HasKeys(track.RotationKeys);
                var hasPosition = HasKeys(track.PositionKeys);
                var hasScale = HasKeys(track.ScaleKeys);
                var hasVisibility = track.VisibilityKeys.Length > 0;
                if (!hasRotation && !hasPosition && !hasScale && !hasVisibility)
                {
                    continue;
                }

                foreach (var node in nodes)
                {
                    if (node.LocalTransform.IsMatrix)
                    {
                        throw new InvalidDataException(
                            $"{FinToGltfConverter.Describe(animated.Node)} is animated but its rest transform isn't a rotation and scale");
                    }
                }

                var times = SampleTicks(plan, FinKeyEvaluator.KeyTimes(track.RotationKeys)
                    .Concat(FinKeyEvaluator.KeyTimes(track.PositionKeys))
                    .Concat(FinKeyEvaluator.KeyTimes(track.ScaleKeys)));
                if (hasPosition)
                {
                    var keys = Sample(plan, times, ticks => FinKeyEvaluator.Position(track.PositionKeys, Time(core, ticks)));
                    channels.AddRange(nodes.Select(node => (Action<GltfAnimation>)(a => a.CreateTranslationChannel(node, keys, true))));
                }
                if (hasRotation)
                {
                    var keys = Sample(plan, times,
                        ticks => Quaternion.Normalize(FinKeyEvaluator.Rotation(track.RotationKeys, Time(core, ticks))));
                    channels.AddRange(nodes.Select(node => (Action<GltfAnimation>)(a => a.CreateRotationChannel(node, keys, true))));
                }
                if (hasRotation || hasScale)
                {
                    // A rotation key replaces the whole rest rotation, a mirrored one included, and the engine's scale
                    // is uniform: write it with every rotation so a mirrored rest scale doesn't stay behind
                    var rest = MathF.Abs(animated.Node.Scale);
                    var keys = Sample(plan, times, ticks => new Vector3(hasScale
                        ? MathF.Abs(FinKeyEvaluator.Float(track.ScaleKeys, Time(core, ticks)))
                        : rest));
                    channels.AddRange(nodes.Select(node => (Action<GltfAnimation>)(a => a.CreateScaleChannel(node, keys, true))));
                }
                if (hasVisibility)
                {
                    // A step at the skill's start and at every key inside it; KHR_node_visibility on the node itself
                    // says how it looks before any skill plays
                    var steps = new SortedSet<float> { plan.StartTicks };
                    steps.UnionWith(track.VisibilityKeys
                        .Select(key => key.Time)
                        .Where(ticks => plan.StartTicks < ticks && ticks <= plan.EndTicks));
                    var keys = Sample(plan, steps.ToList(),
                        ticks => FinKeyEvaluator.Visible(track.VisibilityKeys, Time(core, ticks)));
                    channels.AddRange(nodes.Select(node => (Action<GltfAnimation>)(a =>
                    {
                        node.SetVisibility(!animated.Node.AppCulled);
                        a.CreateVisibilityChannel(node, keys);
                    })));
                }
            }
        }

        // Colour and transparency animators play on every skill's time (DDSkill::Update drives all of an actor's
        // property animators), on every material made from the animated property
        private static void AddMaterialChannels(FinSkillPlan plan, IReadOnlyList<FinMaterialAnimation> animations,
            FinGltfMaterials materials, List<Action<GltfAnimation>> channels)
        {
            foreach (var animation in animations)
            {
                var colors = animation.Material;
                var diffuse = animation.Diffuse is { } d && HasKeys(d.Keys) ? d : null;
                var emissive = animation.Emissive is { } e && HasKeys(e.Keys) ? e : null;
                var alpha = animation.Alpha is { } a && HasKeys(a.Keys) ? a : null;
                foreach (var (material, unlit) in materials.MaterialsWith(colors))
                {
                    // Unlit, only the alpha shows
                    if (alpha is not null || (diffuse is not null && !unlit))
                    {
                        var times = SampleTicks(plan, KeyTimes(diffuse).Concat(alpha is null ? [] : FinKeyEvaluator.KeyTimes(alpha.Keys)));
                        var keys = Sample(plan, times, ticks => FinGltfMaterials.BaseColor(
                            diffuse is null ? FinGltfMaterials.Diffuse(colors) : Color(diffuse, ticks),
                            alpha is null ? colors.Alpha : FinKeyEvaluator.Float(alpha.Keys, Time(alpha.Core, ticks)),
                            unlit));
                        channels.Add(gltf => gltf.CreateMaterialPropertyChannel(material, BASE_COLOR, keys, true));
                    }
                    if (emissive is not null && !unlit)
                    {
                        var times = SampleTicks(plan, KeyTimes(emissive));
                        var keys = Sample(plan, times, ticks => FinGltfMaterials.Emissive(Color(emissive, ticks)));
                        channels.Add(gltf => gltf.CreateMaterialPropertyChannel(material, EMISSIVE, keys, true));
                    }
                }
            }
        }

        // Ni3dsColorAnimator::AnimateUpdate: each channel kept within 0 to 1
        private static Vector3 Color(Ni3dsColorAnimator animator, float ticks)
        {
            var value = FinKeyEvaluator.Position(animator.Keys, Time(animator.Core, ticks));

            return Vector3.Clamp(value, Vector3.Zero, Vector3.One);
        }

        private static IEnumerable<float> KeyTimes(Ni3dsColorAnimator? animator)
        {
            return animator is null ? [] : FinKeyEvaluator.KeyTimes(animator.Keys);
        }

        private static void AddFlipBooks(FinSkillPlan plan, string skill, FinGltfMaterials materials, JsonArray flipBooks)
        {
            foreach (var use in plan.FlipBooks)
            {
                var frameCount = use.Textures.Images.Count;
                var track = FinFlipBookFrames.Track(use.FlipBook, frameCount, plan.StartTicks, Math.Max(plan.StartTicks, plan.EndTicks));
                var images = materials.ImageIndices(use.Textures);
                foreach (var (material, _) in materials.MaterialsWith(use.Textures))
                {
                    flipBooks.Add(new JsonObject
                    {
                        ["skill"] = skill,
                        ["material"] = material.LogicalIndex,
                        ["images"] = new JsonArray(images.Select(i => (JsonNode?)i).ToArray()),
                        ["times"] = new JsonArray(track.Select(e => (JsonNode?)e.Seconds).ToArray()),
                        ["frames"] = new JsonArray(track.Select(e => (JsonNode?)e.Frame).ToArray()),
                    });
                }
            }
        }

        private static void AddMorphChannels(FinSkillPlan plan, Func<NiAVObject, IReadOnlyList<Node>> nodesOf,
            List<Action<GltfAnimation>> channels)
        {
            foreach (var morph in plan.Morphs.Distinct(ReferenceEqualityComparer.Instance).Cast<Ni3dsMorphShape>())
            {
                var nodes = nodesOf(morph).Where(node => node.Mesh is not null).ToList();
                if (nodes.Count == 0 || morph.Targets.Count < 2 || !morph.Keys.Keys.Any(key => key is FinBaryMorphKey))
                {
                    continue;
                }

                var count = morph.Targets.Count;
                var times = SampleTicks(plan, FinKeyEvaluator.KeyTimes(morph.Keys));
                var keys = Sample(plan, times, ticks => MorphWeights(morph, count, Time(morph.Core, ticks)));
                channels.AddRange(nodes.Select(node =>
                    (Action<GltfAnimation>)(a => a.CreateMorphChannel(node, keys, count - 1, true))));
            }
        }

        // The engine's weights for targets 1 and up; a target the keys don't cover stays at 0
        private static float[] MorphWeights(Ni3dsMorphShape morph, int targetCount, float ticks)
        {
            var weights = FinKeyEvaluator.MorphWeights(morph.Keys, ticks);

            return Enumerable.Range(1, targetCount - 1)
                .Select(i => i < weights.Length ? weights[i] : 0)
                .ToArray();
        }

        // The skill's start, every 80 ticks after it, its end, and every key strictly between, in ticks
        private static IReadOnlyList<float> SampleTicks(FinSkillPlan plan, IEnumerable<float> keyTicks)
        {
            var start = plan.StartTicks;
            var end = Math.Max(start, plan.EndTicks);
            var result = new SortedSet<float> { start, end };
            for (var step = 1; start + step * SAMPLE_TICKS < end; step++)
            {
                result.Add(start + step * SAMPLE_TICKS);
            }
            foreach (var ticks in keyTicks)
            {
                if (start < ticks && ticks < end)
                {
                    result.Add(ticks);
                }
            }

            return result.ToList();
        }

        private static Dictionary<float, T> Sample<T>(FinSkillPlan plan, IReadOnlyList<float> times, Func<float, T> value)
        {
            return times.ToDictionary(
                ticks => (ticks - plan.StartTicks) / FinAnimationTime.TICKS_PER_SECOND,
                value);
        }

        private static float Time(FinAnimationCore core, float ticks)
        {
            return FinAnimationTime.ScaledTime(core, ticks);
        }

        private static bool HasKeys<TKey>(FinKeyGroup<TKey> keys)
            where TKey : class
        {
            return keys.Keys.Any(key => key is not null);
        }
    }
}
