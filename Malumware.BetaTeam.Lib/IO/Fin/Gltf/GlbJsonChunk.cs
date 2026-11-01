using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    // Edits the JSON of a glb after SharpGLTF wrote it, for extensions SharpGLTF can't write (OMI physics). The JSON
    // chunk comes first; the chunks after it are copied unchanged.
    internal static class GlbJsonChunk
    {
        private const int HEADER_SIZE = 12;
        private const int CHUNK_HEADER_SIZE = 8;
        private const int ALIGNMENT = 4;
        private const uint MAGIC = 0x46546C67;          // "glTF"
        private const uint JSON_CHUNK = 0x4E4F534A;     // "JSON"
        private const byte JSON_PADDING = 0x20;

        public static byte[] Rewrite(byte[] glb, Action<JsonObject> edit)
        {
            if (glb.Length < HEADER_SIZE + CHUNK_HEADER_SIZE || BinaryPrimitives.ReadUInt32LittleEndian(glb) != MAGIC)
            {
                throw new InvalidDataException("Not a glb");
            }

            var jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(HEADER_SIZE));
            var chunkType = BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(HEADER_SIZE + 4));
            var jsonStart = HEADER_SIZE + CHUNK_HEADER_SIZE;
            if (chunkType != JSON_CHUNK || jsonLength > (uint)(glb.Length - jsonStart))
            {
                throw new InvalidDataException("The glb doesn't start with a JSON chunk");
            }

            var json = JsonNode.Parse(glb.AsSpan(jsonStart, (int)jsonLength)) as JsonObject
                ?? throw new InvalidDataException("The glb's JSON isn't an object");
            edit(json);

            var text = Encoding.UTF8.GetBytes(json.ToJsonString());
            var paddedLength = (text.Length + ALIGNMENT - 1) / ALIGNMENT * ALIGNMENT;
            var rest = glb.AsSpan(jsonStart + (int)jsonLength);
            var result = new byte[jsonStart + paddedLength + rest.Length];

            glb.AsSpan(0, 8).CopyTo(result);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), (uint)result.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(HEADER_SIZE), (uint)paddedLength);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(HEADER_SIZE + 4), JSON_CHUNK);
            text.CopyTo(result, jsonStart);
            result.AsSpan(jsonStart + text.Length, paddedLength - text.Length).Fill(JSON_PADDING);
            rest.CopyTo(result.AsSpan(jsonStart + paddedLength));

            return result;
        }
    }
}
