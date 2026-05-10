namespace Malumware.BetaTeam.Lib.IO.Dds
{
    // DigitalDomain seemingly has "created" their own sound format dubbed "DDS".
    // The suspicion is that this stands for "Digital Domain Sound".
    public enum DdsAudioFormat : ushort
    {
        Pcm = 1
        // Format 3 (IEEE float) also exists, but is not supported.
    }
}
