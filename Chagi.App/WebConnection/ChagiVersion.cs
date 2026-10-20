using System.Net.Http.Json;
namespace ITF_Taekwondo_Gradering.WebConnection
{
    public class ChagiVersion
    {
        public const string BaseUrl = "https://f96nini.github.io/chagi-files/chagi/";
        public const string DataManifestPath = "manifest.json";
        public string? App { get; set; }
        public string? Version { get; set; }
        public string? Updated { get; set; }
    }
}
