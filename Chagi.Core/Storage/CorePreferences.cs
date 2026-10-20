namespace Chagi.Core.Storage;

public interface IPreferenceStore
{
    string Get(string key, string fallback);
    bool ContainsKey(string key);
    void Set(string key, string value);
}

public static class CorePreferences
{
    // One browser app/MAUI process; never a server-wide user preference store.
    public static IPreferenceStore Current { get; set; } = new MemoryPreferenceStore();
}

public sealed class MemoryPreferenceStore : IPreferenceStore
{
    readonly Dictionary<string, string> values = new();
    public string Get(string key, string fallback) => values.GetValueOrDefault(key, fallback);
    public bool ContainsKey(string key) => values.ContainsKey(key);
    public void Set(string key, string value) => values[key] = value;
}
