namespace Malumware.BetaTeam.Lib.IO.Obj
{
    public class WavefrontObjSplitter
    {
        public IReadOnlyList<WavefrontObj> Split(WavefrontObj source)
        {
            var result = new List<WavefrontObj>(source.Objects.Count);

            foreach (var obj in source.Objects)
            {
                var usedIndices = obj.Faces
                    .SelectMany(f => f)
                    .Distinct()
                    .Order()
                    .ToList();

                var indexMap = usedIndices
                    .Select((old, newIdx) => (old, newIdx))
                    .ToDictionary(t => t.old, t => t.newIdx);

                var vertices = usedIndices
                    .Select(i => source.Vertices[i])
                    .ToList();

                var remapped = obj.Name != null
                    ? new WavefrontObjObject(obj.Name)
                    : new WavefrontObjObject();
                remapped.Material = obj.Material;

                foreach (var face in obj.Faces)
                {
                    remapped.AddFace(face.Select(i => indexMap[i]).ToArray());
                }

                result.Add(new WavefrontObj(vertices, [remapped], source.MaterialLibraries.ToList()));
            }

            return result;
        }
    }
}
