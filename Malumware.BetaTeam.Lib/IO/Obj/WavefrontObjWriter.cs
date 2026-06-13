using System.Globalization;
using System.Text;

namespace Malumware.BetaTeam.Lib.IO.Obj
{
    public class WavefrontObjWriter
    {
        public void Write(WavefrontObj obj, string filePath)
        {
            using var stream = File.Create(filePath);
            Write(obj, stream);
        }

        internal static void Write(WavefrontObj obj, Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding.ASCII))
            {
                foreach (var lib in obj.MaterialLibraries)
                {
                    writer.WriteLine($"mtllib {lib}");
                }

                foreach (var v in obj.Vertices)
                {
                    var x = v.X.ToString(CultureInfo.InvariantCulture);
                    var y = v.Y.ToString(CultureInfo.InvariantCulture);
                    var z = v.Z.ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine(
                        v.W != null
                            ? $"v {x} {y} {z} {v.W.Value.ToString(CultureInfo.InvariantCulture)}"
                            : $"v {x} {y} {z}"
                    );
                }

                foreach (var obj2 in obj.Objects)
                {
                    if (obj.Objects.Count > 1 && obj2.Name != null)
                    {
                        writer.WriteLine($"o {obj2.Name}");
                    }

                    if (obj2.Material != null)
                    {
                        writer.WriteLine($"usemtl {obj2.Material}");
                    }

                    foreach (var face in obj2.Faces)
                    {
                        writer.WriteLine($"f {string.Join(' ', face.Select(i => i + 1))}");
                    }
                }
            }
        }
    }
}
