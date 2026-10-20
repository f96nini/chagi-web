using ITF_Taekwondo_Gradering.Models.Clubs;

namespace ITF_Taekwondo_Gradering.Views;

public partial class ClubCompetitionCard : ContentView
{
    public static readonly BindableProperty EntriesProperty =
        BindableProperty.Create(
            nameof(Entries),
            typeof(IReadOnlyList<CompetitionClubStandingEntry>),
            typeof(ClubCompetitionCard),
            default(IReadOnlyList<CompetitionClubStandingEntry>),
            propertyChanged: OnEntriesChanged);

    public ClubCompetitionCard()
    {
        InitializeComponent();
    }

    public IReadOnlyList<CompetitionClubStandingEntry>? Entries
    {
        get => (IReadOnlyList<CompetitionClubStandingEntry>?)GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    static void OnEntriesChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((ClubCompetitionCard)bindable).UpdateEntries();
    }

    void UpdateEntries()
    {
        foreach (var child in RowsHost.Children.Where(child => RowsHost.GetRow(child) > 0).ToList())
            RowsHost.Children.Remove(child);
        while (RowsHost.RowDefinitions.Count > 1)
            RowsHost.RowDefinitions.RemoveAt(RowsHost.RowDefinitions.Count - 1);

        var entries = Entries ?? Array.Empty<CompetitionClubStandingEntry>();
        EmptyStateLabel.IsVisible = entries.Count == 0;

        foreach (var entry in entries)
        {
            var row = RowsHost.RowDefinitions.Count;
            RowsHost.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddRow(entry, row);
        }
    }

    void AddRow(CompetitionClubStandingEntry entry, int row)
    {
        RowsHost.Add(new Label
        {
            Text = entry.Rank.ToString(),
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        }, 0, row);

        RowsHost.Add(new Label
        {
            Text = entry.ClubName,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalTextAlignment = TextAlignment.Center
        }, 1, row);

        AddMedalCounter(1, entry.GoldCount, 2, row);
        AddMedalCounter(2, entry.SilverCount, 4, row);
        AddMedalCounter(3, entry.BronzeCount, 6, row);

        RowsHost.Add(new Label
        {
            Text = entry.TotalPoints.ToString(),
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        }, 8, row);
    }

    void AddMedalCounter(int placement, int count, int column, int row)
    {
        var icon = CompetitionUiHelpers.CreateMedalIcon(placement, 16);
        icon.VerticalOptions = LayoutOptions.Center;
        RowsHost.Add(icon, column, row);
        RowsHost.Add(new Label
        {
            Text = count.ToString(),
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        }, column + 1, row);
    }
}
