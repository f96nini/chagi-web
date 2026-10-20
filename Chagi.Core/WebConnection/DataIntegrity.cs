using System.Security.Cryptography;
using System.Text;

namespace ITF_Taekwondo_Gradering.WebConnection;

public static class DataIntegrity
{
    const string MetadataName = ".chagi-manifest.json";
    public static List<ChagiDataManifestFile> ValidateManifest(ChagiDataManifest manifest)
    {
        if (manifest.Files.Count == 0) throw new InvalidDataException("Tomt manifest.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            file.Path = file.Path.Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(file.Path) || file.Path.Contains(':') || file.Path.Contains('?') || file.Path.Contains('#') ||
                file.Path.Split('/').Any(part => part is "" or "." or "..") || file.Path == MetadataName ||
                !paths.Add(file.Path) || file.Sha256 is not { Length: 64 } || !file.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("Ogiltig filsökväg eller checksumma i manifestet.");
        }
        return manifest.Files;
    }

    public static bool MatchesChecksum(byte[] bytes, ChagiDataManifestFile file)
    {
        bool Matches(byte[] candidate) => string.Equals(Convert.ToHexString(SHA256.HashData(candidate)), file.Sha256, StringComparison.OrdinalIgnoreCase);
        if (Matches(bytes)) return true;
        if (Path.GetExtension(file.Path).ToLowerInvariant() is not (".txt" or ".json")) return false;
        try
        {
            // Git can convert CRLF in the publisher's working tree to LF on Pages.
            // PowerShell can also append a final CRLF to an otherwise LF document.
            // Match those exact UTF-8 representations without changing the stored bytes.
            var utf8 = new UTF8Encoding(false, true);
            var lf = utf8.GetString(bytes).Replace("\r\n", "\n");
            return Matches(utf8.GetBytes(lf)) || Matches(utf8.GetBytes(lf.Replace("\n", "\r\n"))) ||
                lf.EndsWith('\n') && Matches(utf8.GetBytes(lf[..^1] + "\r\n"));
        }
        catch (DecoderFallbackException) { return false; }
    }
}
