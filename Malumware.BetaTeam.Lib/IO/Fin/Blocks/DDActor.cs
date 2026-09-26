namespace Malumware.BetaTeam.Lib.IO.Fin.Blocks
{
    // Digital Domain's game object: a node tree plus a link to the data every instance of that object type shares
    public abstract class DDActor : NiNode
    {
        public FinRef<DDActorSharedData> SharedData { get; private set; } = new(0);

        internal override void Load(FinBlockReader reader)
        {
            base.Load(reader);
            SharedData = reader.ReadRef<DDActorSharedData>();
        }
    }
}
