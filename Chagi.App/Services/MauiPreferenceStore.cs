using Chagi.Core.Storage;

namespace ITF_Taekwondo_Gradering.Services;

public sealed class MauiPreferenceStore : IPreferenceStore
{
    public string Get(string key, string fallback) => Preferences.Get(key, fallback);
    public bool ContainsKey(string key) => Preferences.ContainsKey(key);
    public void Set(string key, string value) => Preferences.Set(key, value);
}
