namespace Malumware.BetaTeam.Lib.IO.Nif.Blocks
{
    public abstract class NiObject
    {
        // Block classes are named after the NIF classes, so the C# name is the name in the file
        public string ClassName => GetType().Name;

        // The blocks this one refers to, in file order; the writer uses them to collect and number every block
        internal virtual IEnumerable<NiObject?> GetLinks()
        {
            return [];
        }

        internal abstract void Write(NifWriter writer);
    }
}
