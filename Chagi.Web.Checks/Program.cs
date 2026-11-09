using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chagi.Core.Data;
using Chagi.Core.Presentation;
using Chagi.Web.Components;
using Chagi.Web.Layout;
using Chagi.Web.Services;
using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.WebConnection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Compile the actual Razor files and render them in-process, with no browser or
// network access. This does not verify WebAssembly boot, CSS layout or JS events.
var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<NavigationManager>(new CheckNavigation());
services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
services.AddSingleton<PublicImages>();
var source = new PublishedResultsClient(new HttpClient(new FixtureHandler()), new Uri("https://fixture.invalid/"), new CheckStore());
services.AddSingleton(source);
using var provider = services.BuildServiceProvider();
await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
async Task<string> Render<T>(Dictionary<string, object?>? parameters = null) where T : IComponent => await renderer.Dispatcher.InvokeAsync(async () => {
    var component = await renderer.RenderComponentAsync<T>(parameters == null ? ParameterView.Empty : ParameterView.FromDictionary(parameters));
    return WebUtility.HtmlDecode(component.ToHtmlString());
});
var count = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS: " + message); }
var home = await Render<Chagi.Web.Pages.Home>();
Check(home.Contains("Testcup") && !home.Contains("Det gick inte att hämta resultaten"), "Home renders published competitions without a false error state");
var fighters = await Render<Chagi.Web.Pages.Fighters>();
Check(fighters.Contains("Test Alpha") && fighters.Contains("Testklubb") && !fighters.Contains("fighter.Team"), "Fighter names and clubs are component values rather than literal binding expressions");
Check(fighters.Contains(">TA</span>") && !fighters.Contains(">F</span>"), "Avatar initials come from the fighter name");
var data = await source.LoadAsync();
var id = ResultLinks.FighterId(data.Fighters.First());
Check(fighters.Contains("fighters/" + id), "Fighter rows contain stable profile links");
var profile = await Render<Chagi.Web.Pages.FighterProfile>(new() { ["Id"] = id });
Check(profile.Contains("Test Alpha") && profile.Contains("Testcup") && profile.Contains("VS · Inbördes möten") && !profile.Contains("Profilen finns inte"), "Profile routes resolve after a new import and link to VS");
Check(profile.Contains("Alla medaljer") && profile.Split("class=\"fighter-medal-discipline\"").Length == 5 &&
      profile.Contains("<h3>Sparring</h3>") && profile.Contains("<h3>Tul</h3>") &&
      profile.Contains("<h3>Spec</h3>") && profile.Contains("<h3>Kross</h3>") &&
      profile.Contains("images/trophy-gold.svg") && profile.Contains("images/trophy-silver.svg") &&
      profile.Contains("images/trophy-bronze.svg"),
    "Fighter profile shows all medals and a trophy breakdown for each discipline");
Check(profile.Contains("class=\"fighter-history-table\"") && profile.Contains("class=\"fighter-history-group\"") &&
      profile.Contains("aria-sort=\"descending\"") && profile.Contains("Gren / klass"),
    "Fighter history renders competition groups with sortable column headers");
var historyResults = new[]
{
    new CompetitionResult { CompetitionDate = new(2026, 1, 1), CompetitionName = "Alfa", CompetitionCity = "Göteborg", Discipline = CompetitionDisciplineE.Tul, Division = "Mönster", Placement = 2, PointsAwarded = 3 },
    new CompetitionResult { CompetitionDate = new(2026, 1, 1), CompetitionName = "Alfa", CompetitionCity = "Göteborg", Discipline = CompetitionDisciplineE.Sparring, Division = "Sparring", Placement = 1, PointsAwarded = 5 },
    new CompetitionResult { CompetitionDate = new(2026, 2, 1), CompetitionName = "Beta", CompetitionCity = "Stockholm", Discipline = CompetitionDisciplineE.Kross, Division = "Kross", Placement = 3, PointsAwarded = 1 },
    new CompetitionResult { CompetitionDate = new(2026, 3, 1), CompetitionName = "Gamma", CompetitionCity = "Ljungby", Discipline = CompetitionDisciplineE.Tul, Division = "Mönster", Placement = 0, PointsAwarded = 0 }
};
var byDate = FighterHistorySortService.Sort(historyResults, FighterHistorySortColumn.Competition, false);
var historyByPoints = FighterHistorySortService.Sort(historyResults, FighterHistorySortColumn.Points, false);
var byPlacement = FighterHistorySortService.Sort(historyResults, FighterHistorySortColumn.Placement, true);
var byDivision = FighterHistorySortService.Sort(historyResults, FighterHistorySortColumn.Division, true);
Check(byDate.Select(group => group.CompetitionName).SequenceEqual(["Gamma", "Beta", "Alfa"]) &&
      historyByPoints.Select(group => group.CompetitionName).SequenceEqual(["Alfa", "Beta", "Gamma"]) &&
      byPlacement.Select(group => group.CompetitionName).SequenceEqual(["Alfa", "Beta", "Gamma"]) &&
      byDivision.Select(group => group.CompetitionName).SequenceEqual(["Beta", "Alfa", "Gamma"]) &&
      historyByPoints[0].Results.Count == 2 && historyByPoints[0].Results.Sum(result => result.PointsAwarded) == 8,
    "Every history sort keeps a competition's results together and orders competition groups correctly");
var previousId = Convert.ToBase64String(Encoding.UTF8.GetBytes(data.Fighters.First().FavoriteKey)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
var previousProfile = await Render<Chagi.Web.Pages.FighterProfile>(new() { ["Id"] = previousId });
Check(previousProfile.Contains("Test Alpha") && !previousProfile.Contains("Profilen finns inte"), "Previously published fighter links still resolve");
Check(profile.Contains("Badges") && profile.Contains("Klubblogotyp för Testklubb") && profile.Contains("images/club-placeholder.svg") && !profile.Contains("Porträtt av Test Alpha"), "Fighter profile offers Badges and uses the club logo in its header");
Check(profile.Contains("Senaste bältesklass:") && profile.Contains("belt-white") && profile.Contains(">Vit</span>") && !profile.Contains("Kup-"), "Fighter profile shows a belt color rather than an inferred Kup grade");
var colorFixture = new Competitor { Results = [
    new() { CompetitionDate = new DateTime(2026, 6, 1), BeltGroup = "RÖD" },
    new() { CompetitionDate = new DateTime(2026, 10, 1), BeltGroup = "GRÖN/BLÅ/RÖD" },
    new() { CompetitionDate = new DateTime(2026, 10, 1), BeltGroup = "BLÅ" }
] };
Check(ProfileBeltColors.FromLatestClass(colorFixture).Select(color => color.Name).SequenceEqual(["Blå"]), "The most specific class on the latest date determines the displayed belt color");
colorFixture.Results.RemoveAt(2);
Check(ProfileBeltColors.FromLatestClass(colorFixture).Select(color => color.Name).SequenceEqual(["Grön", "Blå", "Röd"]), "A combined class keeps all reported colors without inventing one belt");
Check(ProfileBeltColors.FromLatestClass(new Competitor { Results = [new() { BeltGroup = "GUL (8-7 Kup)" }] }).Single().Name == "Gul", "A Kup class is presented as its color only");
var badgeAwards = CompetitorBadgeCalculator.Calculate(data.Fighters.First(), data.Fighters);
Check(badgeAwards.Any(badge => badge.Id == "first-medal" && badge.IconSource == "bronze_large.png") &&
      badgeAwards.Any(badge => badge.Id == "first-gold" && badge.IconSource == "gold_large.png"),
      "Published fighter results unlock the same first-medal and first-gold badges as the app");
var badgeCards = await Render<FighterBadges>(new() { ["Awards"] = badgeAwards });
Check(badgeCards.Contains("Första medaljen") && badgeCards.Contains("images/badges/bronze_large.png") &&
      badgeCards.Contains("Uppnådd") && badgeCards.Contains("Första guldet"), "Badge cards render artwork and award dates");
var emptyBadges = await Render<FighterBadges>();
Check(emptyBadges.Contains("Inga badges upplåsta än"), "A fighter without awards has an explicit empty Badges state");
var vs = await Render<Chagi.Web.Pages.FighterVs>(new() { ["Id"] = id });
Check(vs.Contains("Test Gamma") && vs.Contains("Vinnare: Test Alpha") && vs.Contains("Matchpoäng: 5 – 3") && vs.Contains("Matchdetaljer"), "VS renders a real match with result, opponent and details link");
var competition = await Render<Chagi.Web.Pages.CompetitionDetails>(new() { ["Id"] = ResultLinks.CompetitionId(data.Competitions.First()) });
Check(competition.Contains("<details") && competition.Contains("Test Alpha") && competition.Contains("Klassresultat") && competition.Contains("Visa matchdetaljer"), "Competition details render placements and an available match-details link");
Check(competition.Contains(">Resultat</th>") && competition.Contains("170 cm") && competition.Contains("class=\"number\">500</td>"),
    "Expanded spec and power classes show their measured result beside the existing columns");
var matchDetails = await Render<Chagi.Web.Pages.MatchDetails>(new() { ["Id"] = ResultLinks.CompetitionId(data.Competitions.First()), ["DivisionId"] = ResultLinks.DivisionId("Mönster Pojkar Kadetter VIT") });
Check(matchDetails.Contains("Match 1") && matchDetails.Contains("Test Alpha") && matchDetails.Contains("Test Gamma") && matchDetails.Contains("5 matchpoäng") && matchDetails.Contains("Vinnare"), "Match-details route renders participants, scores and winner");
var sparringDetails = await Render<Chagi.Web.Pages.MatchDetails>(new() { ["Id"] = ResultLinks.CompetitionId(data.Competitions.First()), ["DivisionId"] = ResultLinks.DivisionId("Sparring Pojkar Kadetter VIT") });
Check(sparringDetails.Contains("Sammanställning") && sparringDetails.Contains("Poäng</th>") &&
      sparringDetails.Contains("Vinster</th>") && sparringDetails.Contains("Flaggor</th>") &&
      sparringDetails.IndexOf("Sammanställning", StringComparison.Ordinal) < sparringDetails.IndexOf("Match 1", StringComparison.Ordinal) &&
      sparringDetails.Contains("Test Alpha") && sparringDetails.Contains("Test Gamma"),
    "Sparring match details put the participant summary above the match cards");
var specDetails = await Render<Chagi.Web.Pages.MatchDetails>(new() { ["Id"] = ResultLinks.CompetitionId(data.Competitions.First()), ["DivisionId"] = ResultLinks.DivisionId("Spec. Flickor Kadetter VIT") });
Check(specDetails.Contains("Resultattabell") && specDetails.Contains("Detaljer") &&
      specDetails.Contains("170 cm") && specDetails.Contains("Position</th>") && !specDetails.Contains("Start 1"),
    "Spec details default to a highest-cleared-height table with a details switch");
var powerDetails = await Render<Chagi.Web.Pages.MatchDetails>(new() { ["Id"] = ResultLinks.CompetitionId(data.Competitions.First()), ["DivisionId"] = ResultLinks.DivisionId("Kross Pojkar Kadetter VIT") });
Check(powerDetails.Contains("Position</th>") && powerDetails.Contains("Test Alpha") && powerDetails.Contains("Test Gamma") &&
      !powerDetails.Contains("Start 1") && !powerDetails.Contains("Ring A"),
    "Power details show one compact result table without start cards or ring labels");
var summary = SparringMatchSummaryService.Build([
    new() { Discipline = "SPARRING", MatchId = 1, Finished = 1, RedId = 1, RedParticipant = "Test Alpha - Testklubb", RedPoints = 2, RedFlags = 4,
        BlueId = 2, BlueParticipant = "Test Gamma - Testklubb", BluePoints = 0, BlueFlags = 0, WinnerId = 1 },
    new() { Discipline = "SPARRING", MatchId = 2, Finished = 1, RedId = 2, RedParticipant = "Test Gamma - Testklubb", RedPoints = 1, RedFlags = 2,
        BlueId = 1, BlueParticipant = "Test Alpha - Testklubb", BluePoints = 1, BlueFlags = 2 },
    new() { Discipline = "SPARRING", MatchId = 3, Finished = 0, RedId = 1, RedParticipant = "Test Alpha - Testklubb", RedPoints = 9, RedFlags = 9,
        BlueId = 3, BlueParticipant = "Test Delta - Testklubb", BluePoints = 0, BlueFlags = 0 },
    new() { Discipline = "PATTERN", MatchId = 4, Finished = 1, RedId = 1, RedParticipant = "Test Alpha - Testklubb", RedPoints = 5,
        BlueId = 2, BlueParticipant = "Test Gamma - Testklubb", BluePoints = 0 }
], [new() { CompetitorName = "Test Epsilon", Team = "Testklubb" }]);
Check(summary.Count == 4 && summary[0].Name == "Test Alpha" && summary[0].Points == 3 && summary[0].Wins == 1 && summary[0].Flags == 6 &&
      summary.Single(row => row.Name == "Test Gamma").Points == 1 && summary.Single(row => row.Name == "Test Gamma").Flags == 2 &&
      summary.Single(row => row.Name == "Test Delta").FinishedMatches == 0 &&
      summary.Single(row => row.Name == "Test Epsilon").Points is null,
    "Sparring totals use both corners of finished bouts and include participants without a completed bout");
Check(ResultLinks.DivisionId(" mönster pojkar kadetter vit ") == ResultLinks.DivisionId("Mönster Pojkar Kadetter VIT"), "Match detail links tolerate category whitespace and casing differences");
var unfinished = new DivisionMatch { MatchNumber = 2, Finished = 0, RedId = 1, BlueId = 2, RedParticipant = "Test Alpha - Testklubb", BlueParticipant = "Test Gamma - Testklubb", WinnerId = 1 };
var unfinishedCard = await Render<MatchCard>(new() { ["Match"] = unfinished, ["Matches"] = new[] { unfinished }, ["Fighters"] = data.Fighters });
Check(unfinishedCard.Contains("Ej avslutad") && !unfinishedCard.Contains("alt=\"Vinnare\""), "Unfinished match details never mark a winner");
var spec = CompetitionMatchParser.DeserializeMatches("""
    [{"category":"Spec. Pojkar Miniorer VIT/GUL","discipline":"SPEC_TECH","matchId":42,"matchNumber":1,"finished":1,"redId":1,"redParticipant":"Test Alpha - Testklubb","redPoints":1,"individualScores":[{"techniqueId":1,"attemptNumber":1,"points":1,"cleared":true,"eligible":true},{"techniqueId":2,"attemptNumber":2,"heightCm":160,"points":1,"cleared":true,"eligible":true},{"techniqueId":3,"heightCm":170,"points":0,"cleared":false,"eligible":true},{"techniqueId":4,"heightCm":180,"points":null,"eligible":false}],"highestClearedHeightCm":160}]
    """).Single();
Check(spec.IsIndividualAttempt && spec.HasIndividualResults && !spec.HasDetailedData && spec.IndividualScores.Count == 4,
    "Published spec attempts deserialize and qualify as individual match details");
var specCard = await Render<MatchCard>(new() { ["Match"] = spec, ["Matches"] = new[] { spec }, ["Fighters"] = data.Fighters });
Check(specCard.Contains("Start 1") && specCard.Contains("160 cm") && specCard.Contains("170 cm") && specCard.Contains("Klarad") && specCard.Contains("Ej klarad") &&
      !specCard.Contains("180 cm") && !specCard.Contains("Ingen motståndare"), "Spec details show recorded heights and attempts without inventing an opponent");
var power = new DivisionMatch { Category = "Kross Herrar Seniorer", Discipline = "POWER", MatchId = 43, MatchNumber = 2,
    Finished = 1, RedId = 1, RedParticipant = "Test Alpha - Testklubb", RedPoints = 4547,
    IndividualScores = [new() { TechniqueId = 1, Points = 2599, Eligible = true }, new() { TechniqueId = 2, Points = 1948, Eligible = true }] };
Check(power.IsIndividualAttempt && power.HasIndividualResults, "Power breaking is treated as an individual result even when its source code is POWER");
var powerCard = await Render<MatchCard>(new() { ["Match"] = power, ["Matches"] = new[] { power }, ["Fighters"] = data.Fighters });
Check(powerCard.Contains("4547 poäng") && powerCard.Contains("2599 poäng") && powerCard.Contains("1948 poäng") &&
      !powerCard.Contains("Vinnare:"), "Power details retain the reported result and both source attempts");
static DivisionMatch IndividualStart(string discipline, int id, string name, decimal points, decimal? height = null) => new()
{
    Discipline = discipline, Finished = 1, MatchId = id, RedId = id, RedParticipant = $"{name} - Testklubb",
    RedPoints = points, HighestClearedHeightCm = height,
    IndividualScores = [new() { TechniqueId = 1, Points = points, Eligible = true }]
};
var powerDivision = new CompetitionDivisionEntry { Discipline = CompetitionDisciplineE.Kross, Results =
    [new() { CompetitorName = "Alpha", Team = "Testklubb", Placement = 1 },
     new() { CompetitorName = "Beta", Team = "Testklubb", Placement = 2 },
     new() { CompetitorName = "Gamma", Team = "Testklubb", Placement = 3 },
     new() { CompetitorName = "Delta", Team = "Testklubb", Placement = 0 }] };
var powerStarts = new[] { IndividualStart("POWER", 1, "Alpha", 500), IndividualStart("POWER", 2, "Beta", 400),
    IndividualStart("POWER", 3, "Gamma", 400), IndividualStart("POWER", 4, "Delta", 0) };
var powerTable = IndividualResultRankingService.Build(powerDivision, powerStarts);
Check(powerTable.ShowResultColumn &&
      powerTable.Rows.Select(row => (row.Participant.CompetitorName, row.Result, row.Placement))
          .SequenceEqual(new[] { ("Alpha", (decimal?)500, 1), ("Beta", (decimal?)400, 2),
              ("Gamma", (decimal?)400, 3), ("Delta", (decimal?)0, 0) }),
    "Power results display measured values while preserving Jirugi's medal placements");
var specDivision = new CompetitionDivisionEntry { Discipline = CompetitionDisciplineE.Specialtekniker, Results =
    [new() { CompetitorName = "Alpha", Team = "Testklubb", Placement = 2 },
     new() { CompetitorName = "Beta", Team = "Testklubb", Placement = 1 },
     new() { CompetitorName = "Gamma", Team = "Testklubb", Placement = 3 },
     new() { CompetitorName = "Delta", Team = "Testklubb", Placement = 0 }] };
var specStarts = new[] { IndividualStart("SPEC_TECH", 1, "Alpha", 1, 170), IndividualStart("SPEC_TECH", 2, "Beta", 1, 180),
    IndividualStart("SPEC_TECH", 3, "Gamma", 1), IndividualStart("SPEC_TECH", 4, "Delta", 0) };
var specTable = IndividualResultRankingService.Build(specDivision, specStarts);
Check(specTable.ShowResultColumn &&
      specTable.Rows.Select(row => (row.Result, row.Placement))
          .SequenceEqual(new[] { ((decimal?)170, 2), ((decimal?)180, 1), ((decimal?)null, 3), ((decimal?)null, 0) }),
    "Spec result values leave published medal places unchanged");
var incompleteTable = IndividualResultRankingService.Build(powerDivision, powerStarts.SkipLast(1));
Check(incompleteTable.ShowResultColumn && incompleteTable.Rows.Last().Placement == 0,
    "Incomplete individual records display known values without inventing a medal");
var jirugiDivision = new CompetitionDivisionEntry { Discipline = CompetitionDisciplineE.Specialtekniker, Results =
    [new() { CompetitorName = "Gordana Ceprnja", Team = "Göteborg Phoenix", Placement = 1 },
     new() { CompetitorName = "Rebecca Nickless", Team = "Kokkedal", Placement = 2 },
     new() { CompetitorName = "Alicia Kroon", Team = "Torslanda", Placement = 0 }] };
var jirugiTable = IndividualResultRankingService.Build(jirugiDivision,
    [IndividualStart("SPEC_TECH", 1, "Gordana Ceprnja", 1, 212),
     IndividualStart("SPEC_TECH", 2, "Rebecca Nickless", 1, 210),
     IndividualStart("SPEC_TECH", 3, "Alicia Kroon", 0)]);
Check(jirugiTable.Rows.Select(row => row.Placement).SequenceEqual(new[] { 1, 2, 0 }),
    "Alicia Kroon does not receive a bronze when Jirugi publishes only gold and silver");
var ranking = await Render<Chagi.Web.Pages.Ranking>();
Check(ranking.Contains("Test Alpha") && ranking.Contains("Test Beta") && ranking.Contains("Kadetter") && ranking.Contains("Poängligan") && ranking.Contains("Sortera på") && ranking.Contains("Total Poäng") && ranking.Contains(">Klass</option>") && ranking.Contains(">Klubb</option>") && ranking.Contains(">Herrar</option>") && ranking.Contains(">Damer</option>") && ranking.Contains(">Herrar/Damer</option>") && !ranking.Contains("Säsong"), "Poängligan defaults to combined men and women and total points without a season filter");
var rankingFighters = data.Fighters.Select(f => new Competitor {
    Id = f.Id, FirstName = f.FirstName, LastName = f.LastName, Team = f.Team, IsMale = f.IsMale,
    CurrentAgeClass = f.CurrentAgeClass, LatestBelt = f.LatestBelt, Results = f.Results.ToList()
}).ToList();
var byPoints = RankingDisplayService.BuildGroups(rankingFighters, null, CompetitionDisciplineE.Alla, null, RankingSortMode.TotalPoints);
Check(byPoints.Count == 1 && byPoints[0].Title == "Total Poäng" && byPoints[0].Rows.Count == 3 &&
      byPoints[0].Rows.Select(row => row.Entry.Score).SequenceEqual(byPoints[0].Rows.Select(row => row.Entry.Score).OrderDescending()) &&
      byPoints[0].Rows[0].Entry.PositionLabel == "1", "Total points sorts all selected fighters in one ranked list");
var byClass = RankingDisplayService.BuildGroups(rankingFighters, null, CompetitionDisciplineE.Alla, null, RankingSortMode.Class);
Check(byClass.Count == 2 && byClass.All(group => group.Title.Contains("Kadetter")) &&
      byClass.SelectMany(group => group.Rows).Count() == 3 && byClass.Any(group => group.Label == "Herrar / pojkar") &&
      byClass.Any(group => group.Label == "Damer / flickor"), "Class sorting keeps separate class and gender rankings");
rankingFighters[0].Team = "Ås klubb";
var byClub = RankingDisplayService.BuildGroups(rankingFighters, null, CompetitionDisciplineE.Alla, null, RankingSortMode.Club);
Check(byClub.Count == 2 && byClub[0].Title == "Testklubb" && byClub[1].Title == "Ås klubb" &&
      byClub.SelectMany(group => group.Rows).Count() == 3 && byClub.SelectMany(group => group.Rows).Any(row => row.Entry.PositionLabel == "1"),
      "Club sorting groups clubs alphabetically and retains total ranking positions");
var clubs = await Render<Chagi.Web.Pages.Clubs>();
var clubId = ClubCatalogService.ResolveClub("Testklubb")!.Id;
Check(clubs.Contains("Testklubb") && clubs.Contains("klubbar/" + Uri.EscapeDataString(clubId)) && clubs.Contains("Alla klubbar"), "Club catalog includes a results-only club with a working details link");
var clubDetails = await Render<Chagi.Web.Pages.ClubDetails>(new() { ["Id"] = clubId });
Check(clubDetails.Contains("Testklubb") && clubDetails.Contains("Testcup") && clubDetails.Contains("3 fighters") && clubDetails.Contains("Tävlingar"), "Club profile calculates published fighters and competitions");
var clubCompetition = await Render<Chagi.Web.Pages.ClubCompetition>(new() { ["ClubId"] = clubId, ["CompetitionId"] = ResultLinks.CompetitionId(data.Competitions.First()) });
Check(clubCompetition.Contains("Test Alpha") && clubCompetition.Contains("Test Beta") && clubCompetition.Contains("Alla resultat"), "Club competition page renders its medallists and full-results link");
var knownLogo = await Render<ClubBadge>(new() { ["Name"] = "Stockholm Impact" });
Check(knownLogo.Contains("images/clubs/impact.png") && knownLogo.Contains("klubbar/stockholm-impact"), "Known club badges use app artwork and link to club profiles");
var teamFfLogo = await Render<ClubBadge>(new() { ["Name"] = "Team FF TKD" });
Check(teamFfLogo.Contains("images/clubs/team-ff-tkd.png") && teamFfLogo.Contains("klubbar/gnesta"), "Team FF TKD uses the supplied logo without changing its club identity");
var filtered = await Render<NoMatchFighters>();
Check(filtered.Contains("Inga fighters hittades") && !filtered.Contains("Test Alpha"), "A query with no matching fighter renders an explicit empty state");
var status = await Render<DataStatus>(new() { ["Busy"] = false });
Check(string.IsNullOrWhiteSpace(status), "A loaded page without an error or notice has no false status banner");
var error = await Render<DataStatus>(new() { ["Error"] = "Offline test" });
Check(error.Contains("role=\"alert\"") && error.Contains("Offline test") && error.Contains("Försök igen"), "Data errors expose an alert and retry action");
var loading = await Render<DataStatus>(new() { ["Busy"] = true });
Check(loading.Contains("role=\"status\"") && loading.Contains("Hämtar resultaten"), "Loading exposes an accessible status");
var layout = await Render<MainLayout>();
Check(layout.Contains("Hoppa till innehållet") && layout.Contains("aria-label=\"Huvudmeny\"") && layout.Contains("id=\"main\"") && layout.Contains("klubbar") && layout.Contains("poangligan") && layout.Contains("Poängligan"), "Layout exposes a skip link, named navigation, clubs and Poängligan entries");
Console.WriteLine($"All {count} Razor rendering checks passed.");

sealed class CheckNavigation : NavigationManager
{
    public CheckNavigation() => Initialize("https://fixture.invalid/", "https://fixture.invalid/");
    protected override void NavigateToCore(string uri, bool forceLoad) { }
}
sealed class NoMatchFighters : Chagi.Web.Pages.Fighters
{
    protected override void OnInitialized() { Query = "missing"; base.OnInitialized(); }
}
sealed class CheckStore : IDataSnapshotStore
{
    public Task<PublishedSnapshot?> ReadAsync(CancellationToken token = default) => Task.FromResult<PublishedSnapshot?>(null);
    public Task WriteAsync(PublishedSnapshot snapshot, CancellationToken token = default) => Task.CompletedTask;
}
sealed class FixtureHandler : HttpMessageHandler
{
    readonly byte[] text = Encoding.UTF8.GetBytes("Testcup\n2026-10-17\nTeststad\nMönster Pojkar Kadetter VIT\n1. Test Alpha - Testklubb\n2. Test Gamma - Testklubb\nSparring Pojkar Kadetter VIT\n1. Test Alpha - Testklubb\n2. Test Gamma - Testklubb\nMönster Flickor Kadetter VIT\n2. Test Beta - Testklubb\nSpec. Flickor Kadetter VIT\n0. Test Beta - Testklubb\nKross Pojkar Kadetter VIT\n0. Test Alpha - Testklubb\n0. Test Gamma - Testklubb\n");
    readonly byte[] matchIndex = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { new MatchDataIndexEntry { CompetitionDate = new DateTime(2026, 10, 17), CompetitionName = "Testcup", File = "testcup.json" } }));
    readonly byte[] matches = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { new DivisionMatch {
        Category = "Mönster Pojkar Kadetter VIT", Discipline = "PATTERN", MatchId = 10, MatchNumber = 1, Finished = 1,
        RedId = 1, RedParticipant = "Test Alpha - Testklubb", RedPoints = 5, RedFlags = 2,
        BlueId = 2, BlueParticipant = "Test Gamma - Testklubb", BluePoints = 3, BlueFlags = 1,
        WinnerId = 1, Winner = "Test Alpha - Testklubb", Ring = "A" },
        new DivisionMatch { Category = "Sparring Pojkar Kadetter VIT", Discipline = "SPARRING", MatchId = 14, MatchNumber = 1,
            Finished = 1, RedId = 1, RedParticipant = "Test Alpha - Testklubb", RedPoints = 2, RedFlags = 4,
            BlueId = 2, BlueParticipant = "Test Gamma - Testklubb", BluePoints = 0, BlueFlags = 0,
            WinnerId = 1, Winner = "Test Alpha - Testklubb", Ring = "A" },
        new DivisionMatch { Category = "Spec. Flickor Kadetter VIT", Discipline = "SPEC_TECH", MatchId = 11, MatchNumber = 1,
            Finished = 1, RedId = 3, RedParticipant = "Test Beta - Testklubb", RedPoints = 1, HighestClearedHeightCm = 170,
            IndividualScores = [new() { TechniqueId = 1, HeightCm = 170, Points = 1, Eligible = true, Cleared = true }] },
        new DivisionMatch { Category = "Kross Pojkar Kadetter VIT", Discipline = "POWER", MatchId = 12, MatchNumber = 1,
            Finished = 1, RedId = 1, RedParticipant = "Test Alpha - Testklubb", RedPoints = 500,
            IndividualScores = [new() { TechniqueId = 1, Points = 500, Eligible = true }] },
        new DivisionMatch { Category = "Kross Pojkar Kadetter VIT", Discipline = "POWER", MatchId = 13, MatchNumber = 2,
            Finished = 1, RedId = 2, RedParticipant = "Test Gamma - Testklubb", RedPoints = 500,
            IndividualScores = [new() { TechniqueId = 1, Points = 500, Eligible = true }] } }));
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        var bytes = request.RequestUri!.AbsolutePath switch {
            "/manifest.json" => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new ChagiDataManifest {
                Version = "fixture", Files = [
                    new() { Path = "results/2026-10-17_testcup.txt", Sha256 = Convert.ToHexString(SHA256.HashData(text)) },
                    new() { Path = "matches/index.json", Sha256 = Convert.ToHexString(SHA256.HashData(matchIndex)) },
                    new() { Path = "matches/testcup.json", Sha256 = Convert.ToHexString(SHA256.HashData(matches)) }]
            })),
            "/matches/index.json" => matchIndex,
            "/matches/testcup.json" => matches,
            _ => text
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }
}
