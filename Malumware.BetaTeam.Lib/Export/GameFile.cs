namespace Malumware.BetaTeam.Lib.Export
{
    // One file of the game: Archive is the archive's name without extension ("Map"), Name the file's own name
    public sealed record GameFile(string Archive, string Name, DateTime Modified, Func<byte[]> Read);
}
