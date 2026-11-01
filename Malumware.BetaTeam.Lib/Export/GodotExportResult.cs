namespace Malumware.BetaTeam.Lib.Export
{
    public sealed record GodotExportResult(int Written, int Skipped, int Removed, IReadOnlyList<string> Errors);
}
