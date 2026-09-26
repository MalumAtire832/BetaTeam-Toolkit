namespace Malumware.BetaTeam.Lib.IO.Fin.Dump
{
    public sealed record FinDump(
        string Name,
        int Version,
        IReadOnlyList<FinDumpObject> Roots,
        IReadOnlyList<FinDumpObject> Unreferenced);

    // Field is the property name, or null for list items and roots
    public abstract record FinDumpNode(string? Field);

    // A block (LinkId set), inline extra data (Offset set) or a plain nested object such as a key (neither set)
    public sealed record FinDumpObject(
        string? Field,
        string ClassName,
        uint? LinkId,
        long? Offset,
        IReadOnlyList<FinDumpNode> Children)
        : FinDumpNode(Field);

    // A block that is written in full elsewhere in the dump
    public sealed record FinDumpReference(string? Field, string ClassName, uint LinkId) : FinDumpNode(Field);

    public sealed record FinDumpValue(string? Field, object? Value) : FinDumpNode(Field);

    // A list of objects. Data lists (keys, influences, nested arrays) may be summarised; block and extra data lists
    // make up the tree and never are.
    public sealed record FinDumpList(string? Field, string ElementType, IReadOnlyList<FinDumpNode> Items, bool IsData)
        : FinDumpNode(Field);

    // A list of plain values (vertices, indices), which writers may summarise
    public sealed record FinDumpArray(string? Field, string ElementType, IReadOnlyList<object?> Values)
        : FinDumpNode(Field);
}
