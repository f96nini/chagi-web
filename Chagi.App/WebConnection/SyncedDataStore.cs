namespace ITF_Taekwondo_Gradering.WebConnection;

public static class SyncedDataStore
{
    public static string GetDirectory(string appDataDirectory)
    {
        var data = Path.Combine(appDataDirectory, "Data");
        try
        {
            var generation = File.ReadAllText(Path.Combine(data, "synced-current.txt")).Trim();
            if (Guid.TryParseExact(generation, "N", out _) && Directory.Exists(Path.Combine(data, "synced-generations", generation)))
                return Path.Combine(data, "synced-generations", generation);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return Path.Combine(data, "synced");
    }
}
