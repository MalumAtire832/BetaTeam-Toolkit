using System.Numerics;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // How much one bone moves a vertex, and where the vertex sits relative to that bone
    public sealed record FinSkinInfluence(float Weight, Vector3 Offset, FinRef<Ni3dsBone> Bone);
}
