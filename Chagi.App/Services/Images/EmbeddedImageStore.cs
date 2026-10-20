using System.Globalization;
using System.Reflection;

namespace ITF_Taekwondo_Gradering.Services.Images;

public sealed class EmbeddedImageStore
{
    public const string ResourcePrefix = "Chagi.Images.";
    readonly Assembly _assembly;
    readonly Dictionary<string, SortedDictionary<int, string>> _images = new(StringComparer.OrdinalIgnoreCase);

    public EmbeddedImageStore(Assembly assembly)
    {
        _assembly = assembly;
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith(ResourcePrefix, StringComparison.Ordinal)) continue;
            var name = resource[ResourcePrefix.Length..];
            var extension = Path.GetExtension(name);
            var stem = Path.GetFileNameWithoutExtension(name);
            var separator = stem.LastIndexOf(".scale-", StringComparison.Ordinal);
            if (separator < 0 || !int.TryParse(stem.AsSpan(separator + 7), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var scale) || scale <= 0)
                throw new InvalidDataException($"Invalid embedded image name: {resource}");

            var file = stem[..separator] + extension;
            if (!_images.TryGetValue(file, out var variants))
                _images.Add(file, variants = new());
            variants.Add(scale, resource);
        }
    }

    public IEnumerable<string> FileNames => _images.Keys;

    public bool Contains(string file) => _images.ContainsKey(file);

    public Stream Open(string file, float scale = 1)
    {
        if (!_images.TryGetValue(file, out var variants))
            throw new FileNotFoundException("Embedded image not found.", file);

        var requestedScale = float.IsFinite(scale) ? Math.Max(1, (double)scale * 100) : 100;
        var resource = variants.FirstOrDefault(variant => variant.Key >= requestedScale).Value
            ?? variants.Last().Value;
        return _assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException($"Embedded image stream not found: {resource}");
    }
}
