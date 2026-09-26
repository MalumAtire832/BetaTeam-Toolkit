namespace Malumware.BetaTeam.Lib.IO.Fin.Animation
{
    // A list of keys that all share one interpolation type. A key is null when the type is one the engine can't
    // create (the abstract morph key), in which case it reads nothing for it.
    public sealed record FinKeyGroup<TKey>(uint KeyType, IReadOnlyList<TKey?> Keys) where TKey : class
    {
        public static FinKeyGroup<TKey> Empty { get; } = new(0, []);
    }
}
