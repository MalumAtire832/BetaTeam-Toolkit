namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Whether a node sorts its children (e.g. for transparency) before drawing them. The values follow the engine's
    // SetSortingOn, SetSortingOff and SetSortingDefault.
    public enum NiSortingMode : uint
    {
        On = 0,
        Off = 1,
        Default = 2,
    }
}
