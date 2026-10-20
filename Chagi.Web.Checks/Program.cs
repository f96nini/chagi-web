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
var vs = await Render<Chagi.Web.Pages.FighterVs>(new() { ["Id"] = id });
Check(vs.Contains("Test Gamma") && vs.Contains("Vinnare: Test Alpha") && vs.Contains("Matchpoäng: 5 – 3") && vs.Contains("Matchdetaljer"), "VS renders a real match with result, opponent and details link");
var competition = await Render<Chagi.Web.Pages.CompetitionDetails>(new() { ["Id"] = ResultLinks.CompetitionId(data.Competitions.First()) });
Check(competition.Contains("<details") && competition.Contains("Test Alpha") && competition.Contains("Klassresultat") && competition.Contains("Visa matchdetaljer"), "Competition details render placements and an available match-details link");
var matchDetails = await Render<Chagi.Web.Pages.MatchDetails>(new() { ["Id"] = ResultLinks.CompetitionId(data.Competitions.First()), ["DivisionId"] = ResultLinks.DivisionId("Mönster Pojkar Kadetter VIT") });
Check(matchDetails.Contains("Match 1") && matchDetails.Contains("Test Alpha") && matchDetails.Contains("Test Gamma") && matchDetails.Contains("5 matchpoäng") && matchDetails.Contains("Vinnare"), "Match-details route renders participants, scores and winner");
Check(ResultLinks.DivisionId(" mönster pojkar kadetter vit ") == ResultLinks.DivisionId("Mönster Pojkar Kadetter VIT"), "Match detail links tolerate category whitespace and casing differences");
var unfinished = new DivisionMatch { MatchNumber = 2, Finished = 0, RedId = 1, BlueId = 2, RedParticipant = "Test Alpha - Testklubb", BlueParticipant = "Test Gamma - Testklubb", WinnerId = 1 };
var unfinishedCard = await Render<MatchCard>(new() { ["Match"] = unfinished, ["Matches"] = new[] { unfinished }, ["Fighters"] = data.Fighters });
Check(unfinishedCard.Contains("Ej avslutad") && !unfinishedCard.Contains("alt=\"Vinnare\""), "Unfinished match details never mark a winner");
var ranking = await Render<Chagi.Web.Pages.Ranking>();
Check(ranking.Contains("Test Alpha") && ranking.Contains("Test Beta") && ranking.Contains("Kadetter") && ranking.Contains("Poängligan") && ranking.Contains("Sortera på") && ranking.Contains("Total Poäng") && ranking.Contains(">Klass</option>") && ranking.Contains(">Klubb</option>"), "Poängligan defaults to total points and offers class and club sorting");
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
    readonly byte[] text = Encoding.UTF8.GetBytes("Testcup\n2026-10-17\nTeststad\nMönster Pojkar Kadetter VIT\n1. Test Alpha - Testklubb\n2. Test Gamma - Testklubb\nMönster Flickor Kadetter VIT\n2. Test Beta - Testklubb\n");
    readonly byte[] matchIndex = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { new MatchDataIndexEntry { CompetitionDate = new DateTime(2026, 10, 17), CompetitionName = "Testcup", File = "testcup.json" } }));
    readonly byte[] matches = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { new DivisionMatch {
        Category = "Mönster Pojkar Kadetter VIT", Discipline = "PATTERN", MatchId = 10, MatchNumber = 1, Finished = 1,
        RedId = 1, RedParticipant = "Test Alpha - Testklubb", RedPoints = 5, RedFlags = 2,
        BlueId = 2, BlueParticipant = "Test Gamma - Testklubb", BluePoints = 3, BlueFlags = 1,
        WinnerId = 1, Winner = "Test Alpha - Testklubb", Ring = "A" } }));
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
