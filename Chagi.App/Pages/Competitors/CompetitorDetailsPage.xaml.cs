using System.Globalization;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text;
using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.Views;

namespace ITF_Taekwondo_Gradering;

public partial class CompetitorDetailsPage : ContentPage, IQueryAttributable
{
    Competitor? _competitor;
    List<CompetitorBadgeEntry> _badgeEntries = [];
    Guid? _vsLoadedFor;
    Guid? _vsLoadingFor;
    IReadOnlyList<HeadToHeadSection> _vsSections = [];
    IReadOnlyList<HeadToHeadOpponent> _vsOpponents = [];
    bool _vsGroupByOpponent;
    CancellationTokenSource? _vsRenderCancellation;
    bool _detailsLoaded;
    bool _detailsLoading;
    readonly PageLifetime _lifetime = new();

    public CompetitorDetailsPage()
    {
        InitializeComponent();
        NavigationUiService.UseBackButton(this);
        UpdateVsModeButtons();
    }

    public CompetitorDetailsPage(Competitor competitor)
        : this()
    {
        SetCompetitor(competitor);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("competitorKey", out var value))
        {
            var competitor = NavigationParameterStore.Take<Competitor>(value?.ToString());
            if (competitor != null)
                SetCompetitor(competitor);
        }
    }

    void SetCompetitor(Competitor competitor)
    {
        _competitor = competitor;
        _vsRenderCancellation?.Cancel();
        _detailsLoaded = false;
        _vsLoadedFor = null;
        _vsSections = [];
        _vsOpponents = [];
        VsHost.Children.Clear();
        Title = competitor.FullName;

        CompetitorNameLabel.Text = competitor.FullName;
        CompetitorTeamLabel.Text = competitor.Team;
        CompetitorAgeClassLabel.Text = competitor.LatestCompetitionAgeClass.HasValue
            ? RankingClassFilterService.ToSingularDisplayName(competitor.LatestCompetitionAgeClass.Value)
            : string.Empty;
        CompetitorAgeClassLabel.IsVisible = competitor.LatestCompetitionAgeClass.HasValue;

        var teamImage = ResolveTeamImage(_competitor.Team);
        TeamImageBorder.IsVisible = teamImage != null;
        TeamImage.Source = teamImage;

        ShowTab(CompetitorDetailTab.Summary);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _lifetime.Activate();
        await ResultsDataRefreshService.AppearingAsync(this);
    }

    public async Task RefreshDataAsync()
    {
        var token = _lifetime.Token;
        if (_competitor == null) return;
        if (_detailsLoaded)
        {
            if (VsView.IsVisible) await LoadVsAsync();
            return;
        }
        if (_detailsLoading) return;
        _detailsLoading = true;
        try
        {
            await PageWorkService.RunAsync(this, async () =>
            {
                await PopulateSummaryContentAsync(token);
                await PopulateCompetitionsContentAsync(token);
                await PopulateBadgesContentAsync(token);
                _detailsLoaded = true;
            }, "Sammanställer fighter...", token);
            if (VsView.IsVisible) await LoadVsAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Ladda fighterinformation");
            if (_lifetime.IsCurrent(token))
                await DisplayAlert("Fel", "Kunde inte läsa fighterinformationen. Öppna sidan igen för att försöka på nytt.", "OK");
        }
        finally { if (_lifetime.IsCurrent(token)) _detailsLoading = false; }
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        _lifetime.Deactivate();
        _vsRenderCancellation?.Cancel();
        _detailsLoading = false;
        _vsLoadingFor = null;
        MedalistsOverlay.IsVisible = false;
        base.OnDisappearing();
    }

    void OnSummaryClicked(object? sender, EventArgs e) => ShowTab(CompetitorDetailTab.Summary);

    void OnCompetitionsClicked(object? sender, EventArgs e) => ShowTab(CompetitorDetailTab.Competitions);

    void OnBadgesClicked(object? sender, EventArgs e) => ShowTab(CompetitorDetailTab.Badges);

    async void OnVsClicked(object? sender, EventArgs e)
    {
        ShowTab(CompetitorDetailTab.Vs);
        await LoadVsAsync();
    }

    async void OnVsDisciplineClicked(object? sender, EventArgs e) => await SetVsModeAsync(false);

    async void OnVsOpponentClicked(object? sender, EventArgs e) => await SetVsModeAsync(true);

    async Task SetVsModeAsync(bool groupByOpponent)
    {
        if (_vsGroupByOpponent == groupByOpponent) return;
        _vsGroupByOpponent = groupByOpponent;
        UpdateVsModeButtons();
        if (_vsLoadedFor.HasValue) await RenderVsAsync();
    }

    void UpdateVsModeButtons()
    {
        SetTabButtonState(VsDisciplineButton, !_vsGroupByOpponent);
        SetTabButtonState(VsOpponentButton, _vsGroupByOpponent);
    }

    async Task LoadVsAsync()
    {
        var token = _lifetime.Token;
        try { await PageWorkService.RunAsync(this, () => PopulateVsAsync(token), "Sammanställer inbördes möten...", token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Visa inbördes möten"); }
    }

    async Task PopulateVsAsync(CancellationToken token)
    {
        var fighter = _competitor;
        if (fighter == null || _vsLoadingFor == fighter.Id) return;
        if (_vsLoadedFor == fighter.Id)
        {
            if (_vsRenderCancellation?.IsCancellationRequested == true) await RenderVsAsync();
            return;
        }
        _vsLoadingFor = fighter.Id;
        VsHost.Children.Clear();
        VsHost.Add(new ActivityIndicator { Color = Colors.White, IsRunning = true, HorizontalOptions = LayoutOptions.Center });
        try
        {
            var data = await Task.Run(async () =>
            {
                var sections = await HeadToHeadService.GetAsync(fighter).ConfigureAwait(false);
                return (Sections: sections, Opponents: HeadToHeadService.GroupByOpponent(sections));
            }, token).WaitAsync(token);
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(fighter, _competitor)) return;
            _vsSections = data.Sections;
            _vsOpponents = data.Opponents;
            _vsLoadedFor = fighter.Id;
            await RenderVsAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AppDiagnostics.Log(ex, "Ladda inbördes möten");
            if (!_lifetime.IsCurrent(token) || !ReferenceEquals(fighter, _competitor)) return;
            VsHost.Children.Clear();
            VsHost.Add(new Label { TextColor = Colors.White, Text = "Kunde inte läsa in mötena." });
            var retry = new Button { Text = "Försök igen" };
            retry.Clicked += async (_, _) => await LoadVsAsync();
            VsHost.Add(retry);
        }
        finally
        {
            if (_lifetime.IsCurrent(token) && _vsLoadingFor == fighter.Id) _vsLoadingFor = null;
        }
    }

    async Task RenderVsAsync()
    {
        _vsRenderCancellation?.Cancel();
        _vsRenderCancellation?.Dispose();
        _vsRenderCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _vsRenderCancellation.Token;
        VsHost.Children.Clear();
        try
        {
            if (_vsGroupByOpponent)
            {
                var count = 0;
                foreach (var opponent in _vsOpponents)
                {
                    token.ThrowIfCancellationRequested();
                    VsHost.Add(BuildVsOpponent(opponent, groupByDiscipline: true, token));
                    if (++count % 8 == 0) await Task.Delay(1, token);
                }
                if (_vsOpponents.Count == 0)
                    VsHost.Add(new Label { Text = "Inga inbördes möten hittades.", TextColor = Colors.White });
                return;
            }

            foreach (var section in _vsSections)
            {
                var title = $"{CompetitionUiHelpers.ToDisciplineLabel(section.Discipline)} · {MeetingCountText(section.Opponents.Sum(o => o.MeetingCount))}";
                VsHost.Add(BuildVsExpander(title, () => section.Opponents.Count == 0
                    ? [new Label { Text = "Inga inbördes möten hittades.", FontSize = 13 }]
                    : section.Opponents.Select(opponent => BuildVsOpponent(opponent, groupByDiscipline: false, token)), token));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    View BuildVsOpponent(HeadToHeadOpponent opponent, bool groupByDiscipline, CancellationToken token)
    {
        var title = $"{opponent.Name}\n{opponent.Team} · {opponent.Wins}–{opponent.Losses}";
        return BuildVsExpander(title, () => BuildVsMeetingViews(opponent, groupByDiscipline), token);
    }

    IEnumerable<View> BuildVsMeetingViews(HeadToHeadOpponent opponent, bool groupByDiscipline)
    {
        var notes = new List<string> { MeetingCountText(opponent.MeetingCount) };
        if (opponent.Draws > 0)
            notes.Add($"{opponent.Draws} {(opponent.Draws == 1 ? "oavgjort" : "oavgjorda")}");
        if (opponent.Unknown > 0) notes.Add($"{opponent.Unknown} med okänt resultat");
        yield return new Label { Text = string.Join(" · ", notes), FontSize = 12 };
        if (opponent.FighterLeads || opponent.OpponentLeads)
            yield return new Label { Text = $"🏆 {(opponent.FighterLeads ? _competitor?.FullName : opponent.Name)} leder", FontSize = 12 };

        // Use the same discipline order in both modes, with the newest meeting first.
        foreach (var section in _vsSections)
        {
            var meetings = opponent.Meetings.Where(m => m.Discipline == section.Discipline).OrderByDescending(m => m.Date).ToList();
            if (meetings.Count == 0) continue;
            if (groupByDiscipline)
                yield return new Label
                {
                    Text = CompetitionUiHelpers.ToDisciplineLabel(section.Discipline),
                    FontAttributes = FontAttributes.Bold, FontSize = 16, Margin = new Thickness(0, 8, 0, 0)
                };
            foreach (var meeting in meetings)
            {
                var outcome = meeting.Outcome switch
                {
                    HeadToHeadOutcome.Win => $"Vinnare: {_competitor?.FullName}",
                    HeadToHeadOutcome.Loss => $"Vinnare: {opponent.Name}",
                    HeadToHeadOutcome.Draw => "Oavgjort",
                    _ => "Okänt resultat"
                };
                if (meeting.IsPlacementComparison)
                    outcome += " (utifrån pallplacering)";
                var row = new VerticalStackLayout { Spacing = 3 };
                row.Add(new Label { Text = $"{meeting.Date:yyyy-MM-dd} · {meeting.CompetitionName}", FontSize = 12 });
                row.Add(new Label { Text = outcome, FontSize = 13, FontAttributes = FontAttributes.Bold });
                if (meeting.HasDetailedResult)
                {
                    row.Add(new Label { Text = meeting.Division, FontSize = 12 });
                    var score = $"Matchpoäng: {DivisionMatch.FormatScore(meeting.FighterPoints)} – {DivisionMatch.FormatScore(meeting.OpponentPoints)} · Flaggor: {DivisionMatch.FormatScore(meeting.FighterFlags)} – {DivisionMatch.FormatScore(meeting.OpponentFlags)}";
                    row.Add(new Label { Text = score, FontSize = 12 });
                }
                yield return row;
            }
        }
    }

    static string MeetingCountText(int count) => count == 1 ? "1 möte" : $"{count} möten";

    static View BuildVsExpander(string title, Func<IEnumerable<View>> createDetails, CancellationToken token)
    {
        var content = new VerticalStackLayout { Spacing = 8 };
        var details = new VerticalStackLayout { Spacing = 10, IsVisible = false };
        var detailsBuilt = false;
        var building = false;
        var toggle = new Button
        {
            Text = $"▸ {title}", FontSize = 14, FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap, Padding = new Thickness(12, 10), HorizontalOptions = LayoutOptions.Fill
        };
        SemanticProperties.SetHint(toggle, "Visa möten");
        toggle.Clicked += async (_, _) =>
        {
            details.IsVisible = !details.IsVisible;
            toggle.Text = $"{(details.IsVisible ? "▾" : "▸")} {title}";
            SemanticProperties.SetHint(toggle, details.IsVisible ? "Dölj möten" : "Visa möten");
            if (detailsBuilt || building || !details.IsVisible) return;
            building = true;
            try
            {
                details.Children.Clear();
                var count = 0;
                foreach (var view in createDetails())
                {
                    token.ThrowIfCancellationRequested();
                    details.Add(view);
                    if (++count % 8 == 0) await Task.Delay(1, token);
                }
                detailsBuilt = true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Visa mötesdetaljer"); }
            finally { building = false; }
        };
        content.Add(toggle);
        content.Add(details);
        return CompetitionUiHelpers.CreateCard(content);
    }

    void OnBadgeInfoClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: CompetitorBadgeEntry badgeEntry })
            return;

        var shouldShowInfo = !badgeEntry.IsInfoVisible;
        foreach (var entry in _badgeEntries)
        {
            entry.IsInfoVisible = false;
        }

        badgeEntry.IsInfoVisible = shouldShowInfo;
    }

    async Task PopulateSummaryContentAsync(CancellationToken token)
    {
        var fighter = _competitor;
        if (fighter == null) return;

        var summaries = await Task.Run(async () =>
        {
            var competitors = await CompetitorRepository.GetAllAsync().ConfigureAwait(false);
            return new[]
            {
                FighterMedalSummaryService.Build(fighter, competitors, CompetitionDisciplineE.Alla),
                FighterMedalSummaryService.Build(fighter, competitors, CompetitionDisciplineE.Tul),
                FighterMedalSummaryService.Build(fighter, competitors, CompetitionDisciplineE.Sparring),
                FighterMedalSummaryService.Build(fighter, competitors, CompetitionDisciplineE.Specialtekniker),
                FighterMedalSummaryService.Build(fighter, competitors, CompetitionDisciplineE.Kross)
            };
        }, token);
        token.ThrowIfCancellationRequested();
        TotalMedalsView.Summary = summaries[0];
        TulDisciplineView.Summary = summaries[1];
        SparringDisciplineView.Summary = summaries[2];
        SpecDisciplineView.Summary = summaries[3];
        KrossDisciplineView.Summary = summaries[4];
    }

    async Task PopulateCompetitionsContentAsync(CancellationToken token)
    {
        if (_competitor == null)
            return;

        var cards = await Task.Run(() => _competitor.Results
            .GroupBy(GetCompetitionKey)
            .Select(group => BuildCompetitionCardEntry(group.ToList()))
            .OrderByDescending(card => card.SortDate)
            .Select(card => card.Card)
            .ToList(), token);
        token.ThrowIfCancellationRequested();

        NoCompetitionsLabel.IsVisible = cards.Count == 0;
        var visibleCards = new System.Collections.ObjectModel.ObservableCollection<CompetitionCardEntry>();
        BindableLayout.SetItemsSource(CompetitionsCardsHost, visibleCards);
        foreach (var card in cards)
        {
            visibleCards.Add(card);
            await Task.Delay(1, token);
        }
    }

    async Task PopulateBadgesContentAsync(CancellationToken token)
    {
        if (_competitor == null)
            return;

        var badges = (await CompetitorBadgeService.CalculateAsync(_competitor).WaitAsync(token))
            .Select(badge => new CompetitorBadgeEntry
            {
                IconSource = badge.IconSource,
                Title = badge.Title,
                InfoText = badge.Description,
                AchievedDateText = $"Uppnådd: {badge.AchievedDate:yyyy-MM-dd}"
            })
            .ToList();

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            token.ThrowIfCancellationRequested();
            _badgeEntries = badges;
            NoBadgesLabel.IsVisible = badges.Count == 0;
            var visibleBadges = new System.Collections.ObjectModel.ObservableCollection<CompetitorBadgeEntry>();
            BindableLayout.SetItemsSource(BadgesHost, visibleBadges);
            foreach (var badge in badges)
            {
                visibleBadges.Add(badge);
                await Task.Delay(1, token);
            }
        });
    }

    CompetitionCardListEntry BuildCompetitionCardEntry(List<CompetitionResult> results)
    {
        var firstResult = results[0];
        var competitionTitle = CompetitionUiHelpers.BuildCompetitionTitle(firstResult.CompetitionDate, firstResult.CompetitionName);

        return new CompetitionCardListEntry
        {
            SortDate = firstResult.CompetitionDate,
            Card = new CompetitionCardEntry
            {
                Title = competitionTitle,
                TotalPointsLabel = $"{CalculateCompetitionPoints(results)} poäng",
                City = firstResult.CompetitionCity,
                Results = results
                    .OrderBy(r => r.Discipline)
                    .ThenBy(r => r.Placement > 0 ? r.Placement : int.MaxValue)
                    .Select(result => new CompetitionCardResultRow
                    {
                        MedalImageSource = result.Placement switch
                        {
                            1 => "trophy_gold.png",
                            2 => "trophy_silver.png",
                            3 => "trophy_bronze.png",
                            _ => string.Empty
                        },
                        DisciplineLabel = CompetitionUiHelpers.ToDisciplineLabel(result.Discipline),
                        MedalLabel = ToMedalLabel(result.Placement),
                        BeltGroup = string.IsNullOrWhiteSpace(result.BeltGroup)
                            ? "vit/gul/gron/bla/rod"
                            : result.BeltGroup,
                        CategoryClass = ResolveCompetitionCardCategoryClass(result),
                        CompetitionTitle = competitionTitle,
                        CompetitionCity = firstResult.CompetitionCity,
                        Division = result.Division,
                        TapCommand = new Command(async () => await ShowDivisionMedalistsOverlayAsync(result, competitionTitle))
                    })
                    .ToList()
            }
        };
    }

    static int CalculateCompetitionPoints(IEnumerable<CompetitionResult> results)
    {
        return results.Sum(result => result.Placement switch
        {
            1 => 5,
            2 => 3,
            3 => 1,
            _ => 0
        });
    }

    static string ResolveCompetitionCardCategoryClass(CompetitionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.CategoryClass))
            return result.CategoryClass;

        return ExtractDivisionClassLabel(result.Division);
    }

    static string ExtractDivisionClassLabel(string division)
    {
        if (string.IsNullOrWhiteSpace(division))
            return string.Empty;

        var value = division.Trim();

        foreach (var prefix in new[] { "Lagmönster", "Sparring", "Mönster", "Spec.", "Kross" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].Trim();
                break;
            }
        }

        foreach (var genderPrefix in new[] { "Flickor/Pojkar", "Damer/Herrar", "Pojkar", "Flickor", "Herrar", "Damer" })
        {
            if (value.StartsWith(genderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[genderPrefix.Length..].Trim();
                break;
            }
        }

        value = Regex.Replace(
            value,
            @"\b(?:VIT|GUL|GRÖN|BLÅ|RÖD)(?:/(?:VIT|GUL|GRÖN|BLÅ|RÖD))*\b",
            string.Empty,
            RegexOptions.IgnoreCase);

        value = Regex.Replace(
            value,
            @"[+-]?\d+(?:cm|kg)(?:/[+-]?\d+(?:cm|kg))*",
            string.Empty,
            RegexOptions.IgnoreCase);

        value = Regex.Replace(value, @"\s*/\s*", "/");
        value = Regex.Replace(value, @"\s{2,}", " ").Trim(' ', '-', '/');

        return value;
    }

    void ShowTab(CompetitorDetailTab tab)
    {
        if (tab != CompetitorDetailTab.Badges)
        {
            foreach (var badgeEntry in _badgeEntries)
            {
                badgeEntry.IsInfoVisible = false;
            }
        }

        SummaryView.IsVisible = tab == CompetitorDetailTab.Summary;
        CompetitionsView.IsVisible = tab == CompetitorDetailTab.Competitions;
        BadgesView.IsVisible = tab == CompetitorDetailTab.Badges;
        VsView.IsVisible = tab == CompetitorDetailTab.Vs;

        SetTabButtonState(SummaryButton, tab == CompetitorDetailTab.Summary);
        SetTabButtonState(CompetitionsButton, tab == CompetitorDetailTab.Competitions);
        SetTabButtonState(BadgesButton, tab == CompetitorDetailTab.Badges);
        SetTabButtonState(VsButton, tab == CompetitorDetailTab.Vs);
    }

    async Task ShowDivisionMedalistsOverlayAsync(CompetitionResult selectedResult, string competitionTitle)
    {
        var token = _lifetime.Token;
        try
        {
            var medalistText = await PageWorkService.RunBackgroundAsync(this, async () =>
            {
                var competitors = await CompetitorRepository.GetAllAsync().ConfigureAwait(false);
                var medalists = competitors
                    .SelectMany(competitor => competitor.Results
                        .Where(result =>
                            result.Placement is >= 1 and <= 3 &&
                            string.Equals(result.Division, selectedResult.Division, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(GetCompetitionKey(result), GetCompetitionKey(selectedResult), StringComparison.OrdinalIgnoreCase))
                        .Select(result => new DivisionMedalistEntry
                        {
                            Placement = result.Placement,
                            Name = competitor.FullName
                        }))
                    .OrderBy(entry => entry.Placement)
                    .ThenBy(entry => entry.Name, SwedishNameComparer.Instance)
                    .ToList();

                var listBuilder = new StringBuilder();
                foreach (var medalist in medalists)
                {
                    listBuilder.Append(medalist.Placement)
                        .Append(". ")
                        .AppendLine(medalist.Name);
                }

                return medalists.Count == 0 ? "Inga medaljörer hittades." : listBuilder.ToString().TrimEnd();
            }, "Laddar medaljörer...", token);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                OverlayTitleLabel.Text = competitionTitle;
                OverlaySubtitleLabel.Text = selectedResult.Division;
                OverlaySubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(selectedResult.Division);
                OverlayMedalistsLabel.Text = medalistText;
                MedalistsOverlay.IsVisible = true;
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Visa medaljörer"); }
    }

    void OnOverlayTapped(object? sender, TappedEventArgs e)
    {
        MedalistsOverlay.IsVisible = false;
    }

    void SetTabButtonState(Button button, bool isSelected)
    {
        if (isSelected)
        {
            button.BackgroundColor = Color.FromArgb("#184E77");
            button.TextColor = Colors.White;
        }
        else
        {
            button.BackgroundColor = Colors.Transparent;
            button.SetAppThemeColor(Button.TextColorProperty, ThemeColors.PrimaryTextLight, ThemeColors.PrimaryTextDark);
        }
    }

    string? ResolveTeamImage(string teamName)
    {
        return ClubCatalogService.ResolveClubImage(teamName) ?? ClubCatalogService.PlaceholderImage;
    }

    static string ToMedalLabel(int placement)
    {
        return placement switch
        {
            1 => "Guld",
            2 => "Silver",
            3 => "Brons",
            > 3 => $"Plats {placement}",
            _ => "Deltagit"
        };
    }

    static string GetCompetitionKey(CompetitionResult result)
    {
        return string.Join("|",
            result.CompetitionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            result.CompetitionName ?? string.Empty,
            result.CompetitionCity ?? string.Empty);
    }

}

enum CompetitorDetailTab
{
    Summary,
    Competitions,
    Badges,
    Vs
}

sealed class CompetitorBadgeEntry : INotifyPropertyChanged
{
    bool _isInfoVisible;

    public string IconSource { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string InfoText { get; init; } = string.Empty;
    public string AchievedDateText { get; init; } = string.Empty;
    public bool HasInfo => !string.IsNullOrWhiteSpace(InfoText);
    public bool IsInfoVisible
    {
        get => _isInfoVisible;
        set
        {
            if (_isInfoVisible == value)
                return;

            _isInfoVisible = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

sealed class CompetitionCardListEntry
{
    public DateTime SortDate { get; init; }
    public CompetitionCardEntry Card { get; init; } = new();
}

sealed class DivisionMedalistEntry
{
    public int Placement { get; init; }
    public string Name { get; init; } = string.Empty;
}
