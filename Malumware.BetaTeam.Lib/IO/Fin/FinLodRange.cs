using System.Numerics;

namespace Malumware.BetaTeam.Lib.IO.Fin
{
    // The distances between which a level of detail is shown, measured from Center
    public readonly record struct FinLodRange(float Near, float Far, Vector3 Center);
}
