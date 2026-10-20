using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class ResultsFilterSettings
{
    // Gender belongs to each page: a new filter always starts with H/D.
    public const GenderFilterMode DefaultGender = GenderFilterMode.All;
    const string DisciplineKey = "results_discipline_filter.v1";

    public static CompetitionDisciplineE Discipline
    {
        get => Read(DisciplineKey, CompetitionDisciplineE.Alla);
        set => Write(DisciplineKey, value);
    }

    static T Read<T>(string key, T fallback) where T : struct, Enum
    {
        var saved = Chagi.Core.Storage.CorePreferences.Current.Get(key, string.Empty);
        return Enum.TryParse<T>(saved, out var value) && Enum.IsDefined(value) ? value : fallback;
    }

    static void Write<T>(string key, T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Chagi.Core.Storage.CorePreferences.Current.Set(key, value.ToString());
    }
}
