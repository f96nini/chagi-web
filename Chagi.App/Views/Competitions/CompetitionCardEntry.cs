using System.Windows.Input;

namespace ITF_Taekwondo_Gradering.Views;

public sealed class CompetitionCardEntry
{
    public string Title { get; init; } = string.Empty;
    public string TotalPointsLabel { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public bool HasCity => !string.IsNullOrWhiteSpace(City);
    public IReadOnlyList<CompetitionCardResultRow> Results { get; init; } = Array.Empty<CompetitionCardResultRow>();
}

public sealed class CompetitionCardResultRow
{
    public string MedalImageSource { get; init; } = "bronze.png";
    public bool HasMedal => !string.IsNullOrWhiteSpace(MedalImageSource);
    public string DisciplineLabel { get; init; } = string.Empty;
    public string MedalLabel { get; init; } = string.Empty;
    public string BeltGroup { get; init; } = string.Empty;
    public string CategoryClass { get; init; } = string.Empty;
    public string CompetitionTitle { get; init; } = string.Empty;
    public string CompetitionCity { get; init; } = string.Empty;
    public string Division { get; init; } = string.Empty;
    public ICommand? TapCommand { get; init; }
    public bool HasCategoryClass => !string.IsNullOrWhiteSpace(CategoryClass);
}
