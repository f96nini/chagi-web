using System.Text.Json.Serialization;

namespace ITF_Taekwondo_Gradering.WebConnection;

public sealed class ChagiDataManifest
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("updated")]
    public string? Updated { get; set; }

    [JsonPropertyName("files")]
    public List<ChagiDataManifestFile> Files { get; set; } = new();
}

public sealed class ChagiDataManifestFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
}
