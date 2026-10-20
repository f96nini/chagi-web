using System.Collections.Concurrent;

namespace ITF_Taekwondo_Gradering;

public static class NavigationParameterStore
{
    static readonly ConcurrentDictionary<string, object> Values = new();

    public static string Store(object value)
    {
        var key = Guid.NewGuid().ToString("N");
        Values[key] = value;
        return key;
    }

    public static T? Take<T>(string? key) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return Values.TryRemove(key, out var value) ? value as T : null;
    }
}
