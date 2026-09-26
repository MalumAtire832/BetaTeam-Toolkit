using Malumware.BetaTeam.Lib.IO.Fin.Blocks;

namespace Malumware.BetaTeam.Lib.IO.Fin.Nif
{
    // The texture properties in effect at an object. FIN splits them over separate properties that are inherited
    // separately; NIF has one NiTexturingProperty that holds all of them.
    internal readonly record struct FinNifTextureState(
        NiTextureProperty? Texture,
        NiTextureModeProperty? Mode,
        NiMultiTextureProperty? MultiTexture)
    {
        public bool HasImages => Texture is not null || MultiTexture is not null;
    }
}
