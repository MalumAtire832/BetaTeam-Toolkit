using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // An animated node and the shared-data track the engine gives it
    public sealed record FinAnimatedNode(Ni3dsAnimationNode Node, FinAnimationTrack Track);

    // A flip-book a skill runs, and the texture property whose image it changes
    public sealed record FinFlipBookUse(NiFlipTextures FlipBook, NiTextureProperty Textures);

    // A material whose colours or transparency are animated. The engine drives these on every skill's time.
    public sealed record FinMaterialAnimation(
        NiMaterialProperty Material,
        Ni3dsColorAnimator? Diffuse,
        Ni3dsColorAnimator? Emissive,
        Ni3dsAlphaAnimator? Alpha);

    // What one skill plays: its range in ticks and the animated parts it drives, worked out the way the engine does
    // when it loads an actor (LoadComp.dll DDActor::SetupAnimNodes, DDActorManager::ProcessSkillsAnimNodes,
    // DDActor::SetupArrays_Properties)
    public sealed record FinSkillPlan(
        FinSkill Skill,
        float StartTicks,
        float EndTicks,
        IReadOnlyList<FinAnimatedNode> Nodes,
        IReadOnlyList<FinFlipBookUse> FlipBooks,
        IReadOnlyList<Ni3dsMorphShape> Morphs)
    {
        private const int MAX_DEPTH = 256;
        // The engine's tolerances when it compares a node's pose at a skill's ends with its pose at time 0
        private const float ROTATION_TOLERANCE = 1e-8f;
        private const float POSITION_TOLERANCE = 1e-6f;
        private const float SCALE_TOLERANCE = 1e-6f;
        private const int COLOR_DIFFUSE = 1;
        private const int COLOR_EMISSIVE = 3;

        // A file without an actor (no DDUnit or DDEnv with shared data) has no skills
        public static IReadOnlyList<FinSkillPlan> Build(FinFile file)
        {
            var actor = file.Objects.OfType<DDActor>().FirstOrDefault(a => a.SharedData.Target is not null);
            if (actor is null)
            {
                return [];
            }

            var shared = actor.SharedData.Target!;
            var nodes = AnimationNodes(actor, shared);
            var morphs = Walk(actor).OfType<Ni3dsMorphShape>().ToList();
            var flipBooks = FlipBooksByIndex(file);

            return shared.Skills
                .Select(skill => Plan(skill, nodes, morphs, flipBooks))
                .ToList();
        }

        // DDActor::SetupAnimNodes: a depth-first walk from the actor hands track i to the i-th Ni3dsAnimationNode
        // (bones included). Tracks left over are never used.
        public static IReadOnlyList<FinAnimatedNode> AnimationNodes(NiAVObject actorRoot, DDActorSharedData shared)
        {
            var nodes = Walk(actorRoot).OfType<Ni3dsAnimationNode>().ToList();
            if (nodes.Count > shared.Tracks.Count)
            {
                throw new InvalidDataException(
                    $"{shared.Name} has {shared.Tracks.Count} animation tracks for {nodes.Count} animated nodes");
            }

            return nodes.Select((node, i) => new FinAnimatedNode(node, shared.Tracks[i])).ToList();
        }

        // Every material with a colour or transparency animator attached through Ni3dsPropAnimExtraData. Ambient and
        // specular animators have nothing to drive in glTF, so they're left out.
        public static IReadOnlyList<FinMaterialAnimation> PropertyAnimators(FinFile file)
        {
            var result = new List<FinMaterialAnimation>();
            foreach (var material in file.Objects.OfType<NiMaterialProperty>())
            {
                var animators = material.ExtraData
                    .OfType<Ni3dsPropAnimExtraData>()
                    .Select(extra => extra.Animator.Target)
                    .ToList();
                var colors = animators.OfType<Ni3dsColorAnimator>().ToList();
                var diffuse = colors.FirstOrDefault(c => c.Unknown44 == COLOR_DIFFUSE);
                var emissive = colors.FirstOrDefault(c => c.Unknown44 == COLOR_EMISSIVE);
                var alpha = animators.OfType<Ni3dsAlphaAnimator>().FirstOrDefault();
                if (diffuse is not null || emissive is not null || alpha is not null)
                {
                    result.Add(new FinMaterialAnimation(material, diffuse, emissive, alpha));
                }
            }

            return result;
        }

        private static FinSkillPlan Plan(FinSkill skill, IReadOnlyList<FinAnimatedNode> nodes,
            IReadOnlyList<Ni3dsMorphShape> morphs, IReadOnlyDictionary<int, FinFlipBookUse> flipBooks)
        {
            var start = FinAnimationTime.ToTicks(skill.StartTime);
            var end = FinAnimationTime.ToTicks(skill.EndTime);

            // The file's own list first; the engine then adds every track with data during the skill
            var indices = skill.AnimationNodes
                .Select(index => (int)index)
                .Where(index => index < nodes.Count)
                .ToList();
            for (var i = 0; i < nodes.Count; i++)
            {
                if (!indices.Contains(i) && HasDataDuringSkill(nodes[i], start, end))
                {
                    indices.Add(i);
                }
            }

            return new FinSkillPlan(
                skill,
                start,
                end,
                indices.Select(index => nodes[index]).ToList(),
                skill.Actions
                    .Where(index => flipBooks.ContainsKey(index))
                    .Select(index => flipBooks[index])
                    .ToList(),
                skill.OtherAnimations
                    .Where(index => index < morphs.Count)
                    .Select(index => morphs[index])
                    .ToList());
        }

        // DDActorManager::HasDataDuringSkill: a key inside the skill, or a pose at the skill's start or end that
        // differs from the pose at time 0
        private static bool HasDataDuringSkill(FinAnimatedNode node, float start, float end)
        {
            var track = node.Track;
            var times = FinKeyEvaluator.KeyTimes(track.RotationKeys)
                .Concat(FinKeyEvaluator.KeyTimes(track.PositionKeys))
                .Concat(FinKeyEvaluator.KeyTimes(track.ScaleKeys))
                .Concat(track.VisibilityKeys.Select(key => key.Time));
            if (times.Any(time => start <= time && time <= end))
            {
                return true;
            }

            var rest = Pose(node, 0);

            return (start > 0 && !SamePose(rest, Pose(node, start))) || !SamePose(rest, Pose(node, end));
        }

        private static (Quaternion? Rotation, Vector3? Position, float? Scale, bool? Visible) Pose(FinAnimatedNode node,
            float ticks)
        {
            var track = node.Track;
            var time = FinAnimationTime.ScaledTime(node.Node.Core, ticks);

            return (
                track.RotationKeys.Keys.Any(key => key is not null) ? FinKeyEvaluator.Rotation(track.RotationKeys, time) : null,
                track.PositionKeys.Keys.Any(key => key is not null) ? FinKeyEvaluator.Position(track.PositionKeys, time) : null,
                track.ScaleKeys.Keys.Any(key => key is not null) ? MathF.Abs(FinKeyEvaluator.Float(track.ScaleKeys, time)) : null,
                track.VisibilityKeys.Length > 0 ? FinKeyEvaluator.Visible(track.VisibilityKeys, time) : null);
        }

        private static bool SamePose(
            (Quaternion? Rotation, Vector3? Position, float? Scale, bool? Visible) a,
            (Quaternion? Rotation, Vector3? Position, float? Scale, bool? Visible) b)
        {
            return SameRotation(a.Rotation, b.Rotation)
                && SameVector(a.Position, b.Position)
                && (a.Scale is null || MathF.Abs(a.Scale.Value - b.Scale!.Value) <= SCALE_TOLERANCE)
                && a.Visible == b.Visible;
        }

        // The engine compares quaternions made from the rotation matrices, so q and −q are the same rotation
        private static bool SameRotation(Quaternion? a, Quaternion? b)
        {
            if (a is null)
            {
                return true;
            }

            var p = Canonical(a.Value);
            var q = Canonical(b!.Value);

            return MathF.Abs(p.X - q.X) <= ROTATION_TOLERANCE
                && MathF.Abs(p.Y - q.Y) <= ROTATION_TOLERANCE
                && MathF.Abs(p.Z - q.Z) <= ROTATION_TOLERANCE
                && MathF.Abs(p.W - q.W) <= ROTATION_TOLERANCE;
        }

        private static Quaternion Canonical(Quaternion q)
        {
            return q.W < 0 ? -q : q;
        }

        private static bool SameVector(Vector3? a, Vector3? b)
        {
            if (a is null)
            {
                return true;
            }

            var difference = Vector3.Abs(a.Value - b!.Value);

            return difference.X <= POSITION_TOLERANCE
                && difference.Y <= POSITION_TOLERANCE
                && difference.Z <= POSITION_TOLERANCE;
        }

        // DDActor::SetupArrays_Properties: a texture property with at least two images and Digital Domain's extra
        // data gets its flip-book stored at the extra data's index, which is what a skill's actions refer to
        private static Dictionary<int, FinFlipBookUse> FlipBooksByIndex(FinFile file)
        {
            var result = new Dictionary<int, FinFlipBookUse>();
            foreach (var texture in file.Objects.OfType<NiTextureProperty>())
            {
                var extra = texture.ExtraData.OfType<TexturePropExtraData>().FirstOrDefault();
                if (texture.Images.Count >= 2 && extra?.FlipTextures.Target is { } flipBook)
                {
                    result[extra.Index] = new FinFlipBookUse(flipBook, texture);
                }
            }

            return result;
        }

        // Every object below a root in depth-first pre-order, each place it's listed
        private static IEnumerable<NiAVObject> Walk(NiAVObject root)
        {
            var stack = new Stack<(NiAVObject Block, int Depth)>();
            stack.Push((root, 0));
            while (stack.Count > 0)
            {
                var (block, depth) = stack.Pop();
                if (depth >= MAX_DEPTH)
                {
                    throw new InvalidDataException($"Nodes are nested more than {MAX_DEPTH} levels deep");
                }

                yield return block;
                if (block is NiNode node)
                {
                    foreach (var child in node.Children.Reverse())
                    {
                        if (child.Target is not null)
                        {
                            stack.Push((child.Target, depth + 1));
                        }
                    }
                }
            }
        }
    }
}
