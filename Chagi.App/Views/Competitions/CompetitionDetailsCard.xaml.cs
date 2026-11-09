using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Views;

public partial class CompetitionDetailsCard : ContentView
{
    readonly TapGestureRecognizer _titleTap = new();
    IReadOnlyList<IndividualResultRow> _otherParticipants = [];
    bool _otherRowsBuilt;
    bool _showResultColumn;

    public static readonly BindableProperty HasMatchDataProperty =
        BindableProperty.Create(nameof(HasMatchData), typeof(bool), typeof(CompetitionDetailsCard), false,
            propertyChanged: (bindable, _, _) => ((CompetitionDetailsCard)bindable).UpdateTitleNavigation());

    public bool HasMatchData
    {
        get => (bool)GetValue(HasMatchDataProperty);
        set => SetValue(HasMatchDataProperty, value);
    }

    void UpdateTitleNavigation()
    {
        DetailsArrow.IsVisible = HasMatchData;
        TitleBorder.GestureRecognizers.Remove(_titleTap);
        if (HasMatchData)
            TitleBorder.GestureRecognizers.Add(_titleTap);
    }

    public static readonly BindableProperty DivisionProperty =
        BindableProperty.Create(
            nameof(Division),
            typeof(CompetitionDivisionEntry),
            typeof(CompetitionDetailsCard),
            default(CompetitionDivisionEntry),
            propertyChanged: OnDivisionChanged);

    public static readonly BindableProperty IndividualResultsProperty =
        BindableProperty.Create(nameof(IndividualResults), typeof(IndividualResultTable), typeof(CompetitionDetailsCard), null,
            propertyChanged: (bindable, _, _) => ((CompetitionDetailsCard)bindable).UpdateDivision());

    public IndividualResultTable? IndividualResults
    {
        get => (IndividualResultTable?)GetValue(IndividualResultsProperty);
        set => SetValue(IndividualResultsProperty, value);
    }

    public static readonly BindableProperty CompetitorSelectedCommandProperty =
        BindableProperty.Create(
            nameof(CompetitorSelectedCommand),
            typeof(Command<CompetitionPlacementEntry>),
            typeof(CompetitionDetailsCard));

    public CompetitionDetailsCard()
    {
        InitializeComponent();
        _titleTap.Tapped += OnTitleTapped;
    }

    public static readonly BindableProperty DivisionSelectedCommandProperty =
        BindableProperty.Create(nameof(DivisionSelectedCommand), typeof(Command<CompetitionDivisionEntry>), typeof(CompetitionDetailsCard));

    public Command<CompetitionDivisionEntry>? DivisionSelectedCommand
    {
        get => (Command<CompetitionDivisionEntry>?)GetValue(DivisionSelectedCommandProperty);
        set => SetValue(DivisionSelectedCommandProperty, value);
    }

    void OnTitleTapped(object? sender, TappedEventArgs e)
    {
        if (HasMatchData && Division != null && DivisionSelectedCommand?.CanExecute(Division) == true)
            DivisionSelectedCommand.Execute(Division);
    }

    public CompetitionDivisionEntry? Division
    {
        get => (CompetitionDivisionEntry?)GetValue(DivisionProperty);
        set => SetValue(DivisionProperty, value);
    }

    public Command<CompetitionPlacementEntry>? CompetitorSelectedCommand
    {
        get => (Command<CompetitionPlacementEntry>?)GetValue(CompetitorSelectedCommandProperty);
        set => SetValue(CompetitorSelectedCommandProperty, value);
    }

    static void OnDivisionChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        ((CompetitionDetailsCard)bindable).UpdateDivision();
    }

    void UpdateDivision()
    {
        WinnerRowsHost.Children.Clear();
        OtherParticipantRowsHost.Children.Clear();
        OtherParticipantRowsHost.IsVisible = false;
        OtherParticipantsToggle.IsVisible = false;
        _otherParticipants = [];
        _otherRowsBuilt = false;
        _showResultColumn = false;
        ResultHeader.IsVisible = false;
        if (Division == null)
        {
            TitleLabel.Text = string.Empty;
            BeltIconsHost.Content = null;
            BeltGroupLabel.Text = string.Empty;
            BeltGroupLabel.IsVisible = false;
            return;
        }

        TitleLabel.Text = BuildTitle(Division);
        BeltIconsHost.Content = CompetitionUiHelpers.BuildBeltIcons(Division.BeltGroup);
        BeltGroupLabel.Text = Division.BeltGroup;
        BeltGroupLabel.IsVisible = !string.IsNullOrWhiteSpace(Division.BeltGroup);

        var table = IndividualResults;
        if (table != null && table.Rows.Any(row => !Division.Results.Contains(row.Participant))) table = null;
        _showResultColumn = table?.ShowResultColumn == true;
        ResultHeader.IsVisible = _showResultColumn;
        var rows = table?.Rows ?? Division.Results.Select(result => new IndividualResultRow(result, null, result.Placement)).ToList();
        var ordered = rows.OrderBy(row => row.Placement > 0 ? row.Placement : int.MaxValue).ToList();
        var visible = ordered.Count <= 4 ? ordered : ordered.Where(row => row.Placement is >= 1 and <= 3).ToList();
        foreach (var result in visible)
        {
            WinnerRowsHost.Children.Add(BuildParticipantRow(result));
        }
        _otherParticipants = ordered.Count <= 4 ? [] : ordered.Where(row => row.Placement is not (>= 1 and <= 3)).ToList();
        RefreshOtherParticipants();
    }

    void OnOtherParticipantsClicked(object? sender, EventArgs e)
    {
        if (Division == null || _otherParticipants.Count == 0) return;
        Division.IsOtherParticipantsExpanded = !Division.IsOtherParticipantsExpanded;
        RefreshOtherParticipants();
    }

    void RefreshOtherParticipants()
    {
        var expanded = Division?.IsOtherParticipantsExpanded == true;
        OtherParticipantsToggle.IsVisible = _otherParticipants.Count > 0;
        OtherParticipantsToggle.Text = expanded
            ? $"Dölj övriga deltagare ({_otherParticipants.Count})"
            : $"Visa övriga deltagare ({_otherParticipants.Count})";

        if (expanded && !_otherRowsBuilt)
        {
            foreach (var result in _otherParticipants)
                OtherParticipantRowsHost.Children.Add(BuildParticipantRow(result));
            _otherRowsBuilt = true;
        }
        OtherParticipantRowsHost.IsVisible = expanded && _otherParticipants.Count > 0;
    }

    public static string BuildTitle(CompetitionDivisionEntry division)
    {
        var parts = new List<string>
        {
            CompetitionUiHelpers.ToDisciplineLabel(division.Discipline)
        };

        var genderClassLabel = GetGenderClassLabel(division);
        if (!string.IsNullOrWhiteSpace(genderClassLabel))
        {
            parts.Add(genderClassLabel);
        }

        if (division.AgeClass.HasValue)
        {
            parts.Add(RankingClassFilterService.ToDisplayName(division.AgeClass.Value));
        }

        if (!string.IsNullOrWhiteSpace(division.CategoryClass))
        {
            parts.Add(division.CategoryClass);
        }

        return string.Join(" ", parts);
    }

    static string GetGenderClassLabel(CompetitionDivisionEntry division)
    {
        var source = division.Division;

        if (source.Contains("Flickor/Pojkar", StringComparison.OrdinalIgnoreCase))
            return "Flickor/Pojkar";

        if (source.Contains("Damer/Herrar", StringComparison.OrdinalIgnoreCase))
            return "Damer/Herrar";

        if (source.Contains("Pojkar", StringComparison.OrdinalIgnoreCase))
            return "Pojkar";

        if (source.Contains("Flickor", StringComparison.OrdinalIgnoreCase))
            return "Flickor";

        if (source.Contains("Herrar", StringComparison.OrdinalIgnoreCase))
            return "Herrar";

        if (source.Contains("Damer", StringComparison.OrdinalIgnoreCase))
            return "Damer";

        return string.Empty;
    }

    View BuildParticipantRow(IndividualResultRow rowResult)
    {
        var result = rowResult.Participant;
        var teamLabel = new Label
        {
            Text = result.Team,
            FontSize = 12
        };
        teamLabel.SetAppThemeColor(Label.TextColorProperty, ThemeColors.SecondaryTextLight, ThemeColors.SecondaryTextDark);

        var info = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = result.CompetitorName,
                    FontAttributes = FontAttributes.Bold
                },
                teamLabel
            }
        };

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        row.Add(CompetitionUiHelpers.CreatePlacementBadge(rowResult.Placement), 0, 0);
        row.Add(info, 1, 0);
        if (_showResultColumn)
            row.Add(new Label
            {
                Text = rowResult.Result.HasValue
                    ? $"{DivisionMatch.FormatScore(rowResult.Result)}{(Division?.Discipline == CompetitionDisciplineE.Specialtekniker ? " cm" : "")}" : "–",
                FontAttributes = FontAttributes.Bold,
                VerticalTextAlignment = TextAlignment.Center
            }, 2, 0);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (CompetitorSelectedCommand?.CanExecute(result) == true)
            {
                CompetitorSelectedCommand.Execute(result);
            }

            await Task.CompletedTask;
        };
        row.GestureRecognizers.Add(tap);

        return row;
    }
}
