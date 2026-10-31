using System.Numerics;
using System.Text.Json.Nodes;
using Malumware.BetaTeam.Lib.IO.Fin.Blocks;
using Malumware.BetaTeam.Lib.IO.Tga;
using SharpGLTF.Memory;
using SharpGLTF.Schema2;

namespace Malumware.BetaTeam.Lib.IO.Fin.Gltf
{
    // Builds the glTF materials of one export from the render properties that apply to each shape. Textures are
    // referenced by URI (converted separately to PNG), so models share them instead of each embedding a copy.
    internal sealed class FinGltfMaterials
    {
        // The engine's texture filter modes, in order: nearest, bilinear, trilinear, nearest with nearest mipmap,
        // nearest with linear mipmaps, bilinear with nearest mipmap
        private static readonly (TextureInterpolationFilter Mag, TextureMipMapFilter Min)[] FILTERS =
        [
            (TextureInterpolationFilter.NEAREST, TextureMipMapFilter.NEAREST),
            (TextureInterpolationFilter.LINEAR, TextureMipMapFilter.LINEAR),
            (TextureInterpolationFilter.LINEAR, TextureMipMapFilter.LINEAR_MIPMAP_LINEAR),
            (TextureInterpolationFilter.NEAREST, TextureMipMapFilter.NEAREST_MIPMAP_NEAREST),
            (TextureInterpolationFilter.NEAREST, TextureMipMapFilter.NEAREST_MIPMAP_LINEAR),
            (TextureInterpolationFilter.LINEAR, TextureMipMapFilter.LINEAR_MIPMAP_NEAREST),
        ];

        // The engine's clamp modes, in order: clamp both, clamp s and wrap t, wrap s and clamp t, wrap both
        private static readonly (TextureWrapMode S, TextureWrapMode T)[] WRAPS =
        [
            (TextureWrapMode.CLAMP_TO_EDGE, TextureWrapMode.CLAMP_TO_EDGE),
            (TextureWrapMode.CLAMP_TO_EDGE, TextureWrapMode.REPEAT),
            (TextureWrapMode.REPEAT, TextureWrapMode.CLAMP_TO_EDGE),
            (TextureWrapMode.REPEAT, TextureWrapMode.REPEAT),
        ];

        // Lightmaps are mapped with the shape's second texture set
        private const int LIGHTMAP_TEXTURE_SET = 1;

        // The glb only names its textures; SharpGLTF still wants image content, so every image gets this
        private static readonly byte[] PLACEHOLDER_PNG = new TgaToPngConverter().ToPng(new TgaImage(1, 1, false, [255, 255, 255, 255]));

        private readonly ModelRoot _model;
        private readonly FinGltfExtrasBuilder _extras;
        private readonly string _textureUriPrefix;
        private readonly Dictionary<string, Material> _materials = new();
        private readonly Dictionary<string, Image> _images = new();
        // Every material made, with the properties it was made from, so animations can find what an animated
        // property became
        private readonly List<(Material Material, IReadOnlyList<NiProperty> Properties, bool Unlit)> _created = [];

        public FinGltfMaterials(ModelRoot model, FinGltfExtrasBuilder extras, string textureUriPrefix)
        {
            _model = model;
            _extras = extras;
            _textureUriPrefix = textureUriPrefix;
        }

        // The properties that apply to an object: those of its ancestors, then its own, a lower one replacing a higher
        // one of the same class, as the engine's property state does
        public static IReadOnlyList<NiProperty> Inherit(IReadOnlyList<NiProperty> inherited, NiAVObject block)
        {
            var own = block.Properties
                .Select(e => e.Target)
                .OfType<NiProperty>()
                .ToList();
            if (own.Count == 0)
            {
                return inherited;
            }

            var result = inherited.ToList();
            foreach (var property in own)
            {
                result.RemoveAll(e => e.GetType() == property.GetType());
                result.Add(property);
            }

            return result;
        }

        // A shape without normals can't be lit, so it shows its vertex colours (baked lighting) as they are
        public Material Get(IReadOnlyList<NiProperty> properties, bool unlit)
        {
            var key = $"{unlit}:{string.Join(",", properties.Select(e => e.Offset))}";
            if (!_materials.TryGetValue(key, out var material))
            {
                material = Create(properties, unlit);
                _materials[key] = material;
                _created.Add((material, properties, unlit));
            }

            return material;
        }

        // The materials made with this property among their properties, and whether each is unlit
        public IReadOnlyList<(Material Material, bool Unlit)> MaterialsWith(NiProperty property)
        {
            return _created
                .Where(e => e.Properties.Any(p => ReferenceEquals(p, property)))
                .Select(e => (e.Material, e.Unlit))
                .ToList();
        }

        // The glTF image index of each of a texture property's images, in order; -1 for an image without a file name
        public IReadOnlyList<int> ImageIndices(NiTextureProperty textures)
        {
            return textures.Images
                .Select((_, index) => ImageName(index, textures.Images) is { } name ? UseImage(name).LogicalIndex : -1)
                .ToList();
        }

        // The base colour factor for a material's diffuse colour and alpha (alpha kept within 0 to 1). Unlit, the
        // diffuse colour plays no part: the vertex colours replace the lighting it would feed (inferred).
        internal static Vector4 BaseColor(Vector3 diffuse, float alpha, bool unlit)
        {
            var opacity = Math.Clamp(alpha, 0, 1);

            return unlit
                ? new Vector4(1, 1, 1, opacity)
                : FinGltfColor.ToLinear(diffuse.X, diffuse.Y, diffuse.Z, opacity);
        }

        internal static Vector3 Emissive(Vector3 emittance)
        {
            var color = FinGltfColor.ToLinear(emittance.X, emittance.Y, emittance.Z, 1);

            return new Vector3(color.X, color.Y, color.Z);
        }

        private Material Create(IReadOnlyList<NiProperty> properties, bool unlit)
        {
            var texture = properties.OfType<NiTextureProperty>().FirstOrDefault();
            var mode = properties.OfType<NiTextureModeProperty>().FirstOrDefault();
            var colors = properties.OfType<NiMaterialProperty>().FirstOrDefault();
            var alpha = properties.OfType<NiAlphaProperty>().FirstOrDefault();
            var multiTexture = properties.OfType<NiMultiTextureProperty>().FirstOrDefault();
            var baseImage = ImageName(texture?.Index ?? -1, texture?.Images);
            var lightmap = ImageName(0, multiTexture?.Images);

            var material = _model.CreateMaterial(Path.GetFileNameWithoutExtension(baseImage ?? lightmap ?? "Material"));
            if (unlit)
            {
                material.InitializeUnlit();
            }
            else
            {
                // The engine's lighting has no metal and no gloss beyond its rare specular property
                material.WithPBRMetallicRoughness(Vector4.One, null, null, 0, 1);
            }

            var baseColor = material.FindChannel("BaseColor")!.Value;
            baseColor.Color = colors is null
                ? Vector4.One
                : BaseColor(Diffuse(colors), colors.Alpha, unlit);
            if (baseImage is not null)
            {
                var (mag, min) = mode is not null && mode.FilterMode < FILTERS.Length
                    ? FILTERS[mode.FilterMode]
                    : (TextureInterpolationFilter.DEFAULT, TextureMipMapFilter.DEFAULT);
                var (wrapS, wrapT) = mode is not null && mode.ClampMode < WRAPS.Length
                    ? WRAPS[mode.ClampMode]
                    : (TextureWrapMode.REPEAT, TextureWrapMode.REPEAT);
                baseColor.SetTexture(0, UseImage(baseImage), null, wrapS, wrapT, min, mag);
            }
            if (!unlit && colors is not null)
            {
                var emissive = material.FindChannel("Emissive")!.Value;
                var color = Emissive(new Vector3(colors.Emittance.R, colors.Emittance.G, colors.Emittance.B));
                emissive.Color = new Vector4(color, 1);
            }

            material.Alpha = alpha?.AlphaBlending == true ? AlphaMode.BLEND : AlphaMode.OPAQUE;
            material.Extras = BuildExtras(properties, lightmap);

            return material;
        }

        internal static Vector3 Diffuse(NiMaterialProperty colors)
        {
            return new Vector3(colors.DiffuseColor.R, colors.DiffuseColor.G, colors.DiffuseColor.B);
        }

        // Every property stays in the extras, for what glTF can't say (depth buffer, vertex colour mode, ...). The
        // lightmap has no standard place either: its image is in the image list and the extras point to it.
        private JsonObject BuildExtras(IReadOnlyList<NiProperty> properties, string? lightmap)
        {
            var list = new JsonArray();
            foreach (var property in properties)
            {
                list.Add(_extras.BuildBlock(property));
            }

            var extras = new JsonObject { ["properties"] = list };
            if (lightmap is not null)
            {
                extras["lightmap"] = new JsonObject
                {
                    ["image"] = UseImage(lightmap).LogicalIndex,
                    ["texCoord"] = LIGHTMAP_TEXTURE_SET,
                };
            }

            return extras;
        }

        // The file name of images[index], when there is such an image and it names a file
        private static string? ImageName(int index, IReadOnlyList<FinRef<NiImage>>? images)
        {
            if (images is null || index < 0 || index >= images.Count)
            {
                return null;
            }

            return images[index].Target is { External: true, FileName: { } fileName } ? fileName : null;
        }

        private Image UseImage(string fileName)
        {
            var uri = _textureUriPrefix + AssetFileName.WithExtension(fileName, "png");
            if (!_images.TryGetValue(uri, out var image))
            {
                image = _model.CreateImage(Path.GetFileNameWithoutExtension(uri));
                image.Content = new MemoryImage(PLACEHOLDER_PNG);
                image.AlternateWriteFileName = uri;
                _images[uri] = image;
            }

            return image;
        }
    }
}
