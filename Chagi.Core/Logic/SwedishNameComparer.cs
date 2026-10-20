using System.Globalization;

namespace ITF_Taekwondo_Gradering.Logic;

public static class SwedishNameComparer
{
    // Name order must stay Swedish even when the device uses another locale.
    public static StringComparer Instance { get; } =
        StringComparer.Create(CultureInfo.GetCultureInfo("sv-SE"), ignoreCase: true);
}
