using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.Views;
using Microsoft.Maui.Layouts;

namespace ITF_Taekwondo_Gradering;

public sealed record DivisionMatchNavigation(CompetitionCatalogEntry Competition, CompetitionDivisionEntry Division);

public sealed partial class DivisionMatchesPage : ContentPage, IQueryAttributable
{
    readonly VerticalStackLayout _body = new() { Padding = 16, Spacing = 14 };
    DivisionMatchNavigation? _context;
    bool _loading;
    bool _loaded;
    bool _showSpecResultTable = true;
    readonly PageLifetime _lifetime = new();
    ActivityIndicator? _loadingIndicator;

    public DivisionMatchesPage()
    {
        Title = "Matchdetaljer";
        Content = new ScrollView { Content = _body };
        NavigationUiService.UseBackButton(this);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("divisionKey", out var key))
        {
            var context = NavigationParameterStore.Take<DivisionMatchNavigation>(key?.ToString());
            if (context != null)
            {
                _context = context;
                _loaded = false;
                _showSpecResultTable = true;
                Title = CompetitionDetailsCard.BuildTitle(_context.Division);
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifetime.Activate();
        await ResultsDataRefreshService.AppearingAsync(this);
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        _lifetime.Deactivate();
        _loading = false;
        if (_loadingIndicator != null) _loadingIndicator.IsRunning = false;
        base.OnDisappearing();
    }

    async Task LoadAsync()
    {
        var token = _lifetime.Token;
        var context = _context;
        if (_loading || context == null || token.IsCancellationRequested)
            return;
        _loading = true;
        try
        {
            _body.Children.Clear();
            _body.Add(new Label { Text = Title, FontSize = 22, FontAttributes = FontAttributes.Bold });
            _body.Add(new Label { Text = CompetitionUiHelpers.BuildCompetitionTitle(context.Competition.CompetitionDate, context.Competition.CompetitionName), FontSize = 14 });
            _body.Add(new BeltIconsView { BeltGroup = context.Division.BeltGroup });
            _loadingIndicator = new ActivityIndicator { IsRunning = true, HorizontalOptions = LayoutOptions.Center };
            _body.Add(_loadingIndicator);
            var matches = await CompetitionMatchService.GetMatchesAsync(context.Competition.CompetitionDate,
                context.Competition.CompetitionName, context.Division.Division, token);
            token.ThrowIfCancellationRequested();
            if (matches.Count == 0)
                _body.Add(new Label { Text = "Detaljerade matchuppgifter finns inte för den här klassen. Resultatlistan finns på föregående sida." });
            else
            {
                var individual = matches.All(match => match.IsIndividualAttempt);
                var isPower = context.Division.Discipline == CompetitionDisciplineE.Kross;
                var isSpec = context.Division.Discipline == CompetitionDisciplineE.Specialtekniker;
                var tableRows = isPower || isSpec
                    ? IndividualAttemptTableService.Build(matches, isSpec) : [];
                var countLabel = isPower
                    ? tableRows.Count == 1 ? "1 deltagare" : $"{tableRows.Count} deltagare"
                    : individual
                        ? matches.Count == 1 ? "1 deltagarstart" : $"{matches.Count} deltagarstarter"
                        : matches.Count == 1 ? "1 möte" : $"{matches.Count} möten";
                _body.Add(new Label { Text = countLabel, FontAttributes = FontAttributes.Bold });
                if (isPower)
                    _body.Add(BuildResultTable(tableRows, false));
                else if (isSpec)
                {
                    var buttons = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8 };
                    var resultButton = new Button { Text = "Resultattabell", CornerRadius = 8 };
                    var detailsButton = new Button { Text = "Detaljer", CornerRadius = 8 };
                    buttons.Add(resultButton, 0, 0);
                    buttons.Add(detailsButton, 1, 0);
                    _body.Add(buttons);

                    var table = BuildResultTable(tableRows, true);
                    var details = new VerticalStackLayout { Spacing = 14 };
                    details.Add(new Label { Text = matches.Any(match => match.HasIndividualResults)
                        ? "Registrerade poäng och försök visas nedan. Medaljplaceringarna finns i resultatlistan."
                        : "Individuella teknikvärden saknas i underlaget. Registrerade starter visas nedan; medaljplaceringarna finns i resultatlistan." });
                    foreach (var match in matches)
                    {
                        details.Add(BuildMatchCard(match, matches));
                        await Task.Delay(1, token);
                    }
                    _body.Add(table);
                    _body.Add(details);
                    void SelectView(bool showTable)
                    {
                        _showSpecResultTable = showTable;
                        table.IsVisible = showTable;
                        details.IsVisible = !showTable;
                        SetTabButtonState(resultButton, showTable);
                        SetTabButtonState(detailsButton, !showTable);
                    }
                    resultButton.Clicked += (_, _) => SelectView(true);
                    detailsButton.Clicked += (_, _) => SelectView(false);
                    SelectView(_showSpecResultTable);
                }
                else
                {
                    if (individual)
                        _body.Add(new Label { Text = matches.Any(match => match.HasIndividualResults)
                            ? "Registrerade poäng och försök visas nedan. Medaljplaceringarna finns i resultatlistan."
                            : "Individuella teknikvärden saknas i underlaget. Registrerade starter visas nedan; medaljplaceringarna finns i resultatlistan." });
                    foreach (var match in matches)
                    {
                        _body.Add(BuildMatchCard(match, matches));
                        await Task.Delay(1, token);
                    }
                }
            }
            _loaded = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Ladda matchdetaljer");
            if (!_lifetime.IsCurrent(token)) return;
            _body.Add(new Label { Text = "Matchuppgifterna kunde inte läsas. Försök igen eller uppdatera tävlingsdata under Inställningar." });
            var retry = new Button { Text = "Försök igen" };
            retry.Clicked += async (_, _) => await LoadAsync();
            _body.Add(retry);
        }
        finally
        {
            if (_lifetime.IsCurrent(token))
            {
                if (_loadingIndicator != null)
                {
                    _loadingIndicator.IsRunning = false;
                    _body.Children.Remove(_loadingIndicator);
                    _loadingIndicator = null;
                }
                _loading = false;
            }
        }
    }

    static View BuildResultTable(IReadOnlyList<IndividualAttemptTableRow> rows, bool isSpec)
    {
        var table = new Grid { ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }, ColumnSpacing = 10, RowSpacing = 10 };
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        table.Add(new Label { Text = "Position", FontSize = 12, FontAttributes = FontAttributes.Bold }, 0, 0);
        table.Add(new Label { Text = "Namn", FontSize = 12, FontAttributes = FontAttributes.Bold }, 1, 0);
        table.Add(new Label { Text = "Resultat", FontSize = 12, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.End }, 2, 0);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var gridRow = index + 1;
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            table.Add(new Label { Text = row.Position > 0 ? row.Position.ToString() : "–", FontAttributes = FontAttributes.Bold }, 0, gridRow);
            table.Add(new Label { Text = row.Name, LineBreakMode = LineBreakMode.WordWrap }, 1, gridRow);
            var result = row.Best.HasValue ? DivisionMatch.FormatScore(row.Best) : "–";
            if (row.HasSecondResult)
                result += $" ({DivisionMatch.FormatScore(row.Worst)})";
            if (isSpec && row.Best.HasValue)
                result += " cm";
            table.Add(new Label { Text = result, HorizontalTextAlignment = TextAlignment.End, FontAttributes = FontAttributes.Bold }, 2, gridRow);
        }
        return CompetitionUiHelpers.CreateCard(table);
    }

    static void SetTabButtonState(Button button, bool selected)
    {
        if (selected)
        {
            button.BackgroundColor = Color.FromArgb("#184E77");
            button.TextColor = Colors.White;
        }
        else
        {
            button.SetAppThemeColor(Button.BackgroundColorProperty, ThemeColors.CardStrokeLight, ThemeColors.SurfaceDark);
            button.SetAppThemeColor(Button.TextColorProperty, ThemeColors.PrimaryTextLight, ThemeColors.PrimaryTextDark);
        }
    }

    static View BuildMatchCard(DivisionMatch match, IReadOnlyList<DivisionMatch> matches)
    {
        var content = new VerticalStackLayout { Spacing = 10 };
        var title = match.IsIndividualAttempt ? $"Start {match.MatchNumber}" : $"Match {match.MatchNumber}";
        if (match.ExtraMatchId.HasValue)
        {
            var original = matches.FirstOrDefault(item => item.MatchId == match.ExtraMatchId);
            title += original == null ? " · Extramatch" : $" · Extramatch till match {original.MatchNumber}";
        }
        content.Add(new Label { Text = title, FontAttributes = FontAttributes.Bold, FontSize = 16 });
        var metadata = new List<string>();
        if (!string.IsNullOrWhiteSpace(match.Ring)) metadata.Add($"Ring {match.Ring}");
        if (match.StartTime.HasValue) metadata.Add(match.StartTime.Value.ToString("HH:mm"));
        if (!string.IsNullOrWhiteSpace(match.Group) && match.Group != "0") metadata.Add($"Grupp {match.Group}");
        if (metadata.Count > 0) content.Add(new Label { Text = string.Join(" · ", metadata), FontSize = 13 });

        var showWinner = !match.IsIndividualAttempt && match.Finished == 1 && match.Outcome != "Oavgjort";
        bool IsWinner(int? id, string? participant) => showWinner && (match.WinnerId.HasValue
            ? match.WinnerId == id
            : !string.IsNullOrWhiteSpace(match.Winner) && match.Winner == participant);
        var redWinner = IsWinner(match.RedId, match.RedParticipant);
        var blueWinner = match.HasBlueParticipant && IsWinner(match.BlueId, match.BlueParticipant);
        content.Add(BuildParticipant(match.RedParticipant, "Röd", "#B42332", match.RedPoints, match.RedFlags,
            redWinner, !match.IsIndividualAttempt));
        if (match.HasBlueParticipant)
            content.Add(BuildParticipant(match.BlueParticipant, "Blå", "#1765AD", match.BluePoints, match.BlueFlags,
                blueWinner, !match.IsIndividualAttempt));
        else if (!match.IsIndividualAttempt)
            content.Add(new Label { Text = "Ingen motståndare angiven i underlaget.", FontSize = 13 });
        if (match.HasIndividualResults)
        {
            var summary = new VerticalStackLayout { Spacing = 3 };
            summary.Add(new Label { Text = $"Registrerat resultat: {DivisionMatch.FormatScore(match.RedPoints)} poäng",
                FontAttributes = FontAttributes.Bold });
            if (match.HighestClearedHeightCm is { } height)
                summary.Add(new Label { Text = $"Högsta klarade höjd: {DivisionMatch.FormatScore(height)} cm", FontSize = 13 });
            content.Add(summary);
            foreach (var score in match.IndividualScores.Where(score => score.Eligible && score.Points.HasValue))
            {
                var label = match.Discipline == "SPEC_TECH" && score.HeightCm is { } scoreHeight
                    ? $"Försök {score.AttemptNumber ?? score.TechniqueId} · {DivisionMatch.FormatScore(scoreHeight)} cm"
                    : $"Försök {score.AttemptNumber ?? score.TechniqueId}";
                var value = match.Discipline == "SPEC_TECH" && score.Cleared.HasValue
                    ? score.Cleared.Value ? "Klarad" : "Ej klarad"
                    : $"{DivisionMatch.FormatScore(score.Points)} poäng";
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10 };
                row.Add(new Label { Text = label, FontSize = 13, LineBreakMode = LineBreakMode.WordWrap }, 0, 0);
                row.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0);
                content.Add(row);
            }
        }
        if (!match.IsIndividualAttempt && !redWinner && !blueWinner)
            content.Add(new Label { Text = match.Outcome, FontSize = 13, FontAttributes = FontAttributes.Bold });
        return CompetitionUiHelpers.CreateCard(content);
    }

    static View BuildParticipant(string? participant, string side, string color, decimal? points, decimal? flags, bool winner, bool showScores)
    {
        var stack = new VerticalStackLayout { Spacing = 4 };
        var identity = participant?.Split(" - ", 2) ?? [];
        var heading = new Label
        {
            Text = showScores ? $"⚑ {side}" : "Deltagare",
            TextColor = Color.FromArgb(color), FontAttributes = FontAttributes.Bold, FontSize = 13
        };
        // Use a lighter side color against the dark card background.
        heading.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb(color), Color.FromArgb(side == "Röd" ? "#FF9DA4" : "#8CC8FF"));
        stack.Add(heading);
        var name = new Label
        {
            Text = identity.Length > 0 && identity[0] != "?" ? identity[0] : "Deltagare ej angiven",
            FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap
        };
        if (winner)
        {
            var nameRow = new FlexLayout { Direction = FlexDirection.Row, AlignItems = FlexAlignItems.Center };
            FlexLayout.SetShrink(name, 1);
            nameRow.Children.Add(name);
            var trophy = new Image
            {
                Source = "trophy_gold.png", WidthRequest = 24, HeightRequest = 24,
                Aspect = Aspect.AspectFit, Margin = new Thickness(6, 0, 0, 0)
            };
            FlexLayout.SetShrink(trophy, 0);
            SemanticProperties.SetDescription(trophy, "Vinnare");
            nameRow.Children.Add(trophy);
            stack.Add(nameRow);
        }
        else stack.Add(name);
        if (identity.Length > 1) stack.Add(new Label { Text = identity[1], FontSize = 13 });
        if (showScores)
        {
            var scores = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 12 };
            scores.Add(new Label { Text = $"{DivisionMatch.FormatScore(points)} matchpoäng", FontSize = 16, FontAttributes = FontAttributes.Bold }, 0, 0);
            scores.Add(new Label { Text = $"{DivisionMatch.FormatScore(flags)} {(flags == 1 ? "flagga" : "flaggor")}", FontSize = 16 }, 1, 0);
            stack.Add(scores);
        }
        return new Border
        {
            Stroke = Color.FromArgb(color), StrokeThickness = winner ? 2 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = 10, Content = stack
        };
    }
}
