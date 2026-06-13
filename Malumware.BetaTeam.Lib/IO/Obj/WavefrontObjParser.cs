using Malumware.BetaTeam.Lib.IO.Parsers;

namespace Malumware.BetaTeam.Lib.IO.Obj
{
    public class WavefrontObjParser : LineParser<WavefrontObj>
    {
        public WavefrontObjParser(Stream stream) : base(stream) { }

        public override WavefrontObj Parse()
        {
            var vertices = new List<WavefrontObjVertex>();
            var objects = new List<WavefrontObjObject>();
            var materialLibraries = new List<string>();
            var currentObject = new WavefrontObjObject();
            objects.Add(currentObject);

            while (!Reader.EndOfStream)
            {
                var line = Reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                switch (parts[0])
                {
                    case "v":
                        vertices.Add(ParseVertex(parts));
                        break;
                    case "o":
                        currentObject = new WavefrontObjObject(string.Join(' ', parts[1..]));
                        objects.Add(currentObject);
                        break;
                    case "f":
                        currentObject.AddFace(ParseFace(parts));
                        break;
                    case "usemtl":
                        currentObject.Material = parts[1];
                        break;
                    case "mtllib":
                        materialLibraries.Add(parts[1]);
                        break;
                }
            }

            if (objects[0].Name == null && objects[0].Faces.Count == 0)
            {
                objects.RemoveAt(0);
            }

            return new WavefrontObj(vertices, objects, materialLibraries);
        }

        private static WavefrontObjVertex ParseVertex(string[] parts)
        {
            var x = decimal.Parse(parts[1]);
            var y = decimal.Parse(parts[2]);
            var z = decimal.Parse(parts[3]);
            decimal? w = parts.Length > 4 ? decimal.Parse(parts[4]) : null;
            return new WavefrontObjVertex(x, y, z, w);
        }

        private static int[] ParseFace(string[] parts)
        {
            var indexes = new int[parts.Length - 1];
            for (var i = 1; i < parts.Length; i++)
            {
                var slash = parts[i].IndexOf('/');
                var token = slash >= 0 ? parts[i][..slash] : parts[i];
                indexes[i - 1] = int.Parse(token) - 1;
            }
            return indexes;
        }
    }
}
