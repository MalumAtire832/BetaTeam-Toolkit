using Malumware.BetaTeam.Lib.IO.Nif;

namespace Malumware.BetaTeam.Lib.IO.Fin.Nif
{
    // The converted file, and what couldn't be carried over. Repeated warnings are merged, with a count.
    public sealed record FinNifConversion(NifFile File, IReadOnlyList<string> Warnings);
}
