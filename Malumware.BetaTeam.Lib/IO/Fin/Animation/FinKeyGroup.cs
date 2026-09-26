namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // A list of keys that all share one interpolation type
    public sealed record FinKeyGroup<TKey>(uint KeyType, IReadOnlyList<TKey?> Keys)
        where TKey : class
    {
        public static FinKeyGroup<TKey> Empty { get; } = new(0, []);
    }
}
