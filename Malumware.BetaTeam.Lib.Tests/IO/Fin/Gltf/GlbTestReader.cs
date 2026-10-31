using System.Text;
using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO.Tga;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;

namespace Malumware.BetaTeam.Lib.Tests.IO.Fin.Gltf
{
    // Reads converted glbs back. Their textures are separate files the tests don't have, so every image URI is served
    // a placeholder PNG; the URIs themselves come from the glb's JSON.
    internal static class GlbTestReader
    {
        private static readonly byte[] PLACEHOLDER_PNG = new TgaToPngConverter().ToPng(new TgaImage(1, 1, false, [0, 0, 0, 255]));

        public static ModelRoot Read(byte[] glb)
        {
            var context = ReadContext
                .Create(_ => PLACEHOLDER_PNG)
                .WithSettingsFrom(new ReadSettings { Validation = ValidationMode.Strict });
            using (var stream = new MemoryStream(glb))
            {
                return context.ReadBinarySchema2(stream);
            }
        }

        public static JsonObject Json(byte[] glb)
        {
            var length = BitConverter.ToInt32(glb, 12);

            return JsonNode.Parse(Encoding.UTF8.GetString(glb, 20, length))!.AsObject();
        }

        // The URI of every image, by logical index
        public static IReadOnlyList<string> ImageUris(byte[] glb)
        {
            var images = Json(glb)["images"]?.AsArray() ?? [];

            return images
                .Select(e => e!["uri"]!.GetValue<string>())
                .ToList();
        }
    }
}
