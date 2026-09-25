using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // A plane as normal and constant. Stored as the engine saved it, which isn't always normalised.
    public readonly record struct FinPlane(Vector3 Normal, float Constant);
}
