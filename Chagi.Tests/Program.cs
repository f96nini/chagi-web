using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chagi.Core.Data;
using Chagi.Core.Presentation;
using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.WebConnection;

var count = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    count++;
    Console.WriteLine("PASS: " + message);
}
async Task Fails(Func<Task> action, string message)
{
    try { await action(); } catch (InvalidDataException) { Check(true, message); return; }
    throw new Exception(message);
}

var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
var server = new FixtureServer();
for (var month = 1; month <= 4; month++)
    server.Set($"results/2026-0{month}-01_fixture.txt", $"Fixture {month}\n2026-0{month}-01\nTeststad\nMönster Pojkar Kadetter VIT\n1. Test Alpha - Billdal\n2. Test Beta - Frölunda\n3. Test Gamma - Frölunda\n3. Test Delta - Frölunda\n4. Test Echo - Frölunda\n0. Test Zeta - Frölunda\n");
server.Set("matches/index.json", "[]");
using var http = new HttpClient(server);
var store = new MemorySnapshotStore();
var source = new PublishedResultsClient(http, new Uri("https://fixture.invalid/chagi/"), store, () => now);
var latest = await source.LoadAsync(latestOnly: true);
Check(latest.Competitions.Count == 3 && server.Requests.Count == 5 && !server.Requests.Contains("results/2026-01-01_fixture.txt"), "Home downloads three result files and match index, without the full archive");
Check(latest.Fighters.Single(f => f.FirstName == "Test" && f.LastName == "Alpha").TotalPoints == 15, "Published text is parsed by the existing 5/3/1 implementation");
var before = server.Requests.Count;
var all = await source.LoadAsync();
Check(all.Competitions.Count == 4 && server.Requests.Count == before + 1, "Opening the archive downloads only the missing result file");
Check(all.Fighters.Single(f => f.LastName == "Alpha").Team == "GBG Kampsport", "Web imports retain the mobile club alias normalization");
Check(all.Fighters.Single(f => f.LastName == "Zeta").TotalPoints == 0 && all.Fighters.Single(f => f.LastName == "Echo").TotalPoints == 0, "Participation and fourth place award zero points");
var ranks = RankingService.BuildAllSections(all.Fighters, true, CompetitionDisciplineE.Alla, CompetitorSortE.TotalPoints).Single().Entries;
Check(ranks.Select(r => r.Score).SequenceEqual(new[] { 20, 12, 4, 4, 0, 0 }), "The shared ranking orders 5/3/1 scores");
Check(ranks.Select(r => r.Position).SequenceEqual(new[] { 1, 2, 3, 3, 5, 5 }), "Equal score and medal counts share positions and skip subsequent ranks");
before = server.Requests.Count;
await source.LoadAsync();
Check(server.Requests.Count == before, "Repeated visits within fifteen minutes use verified cached files without HTTP");
now += TimeSpan.FromMinutes(16);
await source.LoadAsync();
Check(server.Requests.Count == before + 1 && server.Requests[^1] == "manifest.json", "A due check revalidates the manifest without redownloading unchanged data");
server.Set("results/2026-04-01_fixture.txt", "Fixture 4\n2026-04-01\nTeststad\nMönster Pojkar Kadetter VIT\n1. Test New - Testklubb\n");
before = server.Requests.Count;
var updated = await source.LoadAsync(refresh: true);
Check(server.Requests.Count == before + 2 && updated.Fighters.Any(f => f.LastName == "New"), "A forced update downloads only the manifest and changed file");
var committed = store.Snapshot;
server.BadBody = "results/2026-04-01_fixture.txt";
server.Set(server.BadBody, "changed again");
var stale = await source.LoadAsync(refresh: true);
Check(stale.IsStale && stale.Fighters.Any(f => f.LastName == "New") && ReferenceEquals(store.Snapshot, committed), "A bad checksum preserves the complete committed snapshot and flags stale data");
server.BadBody = null;
server.Offline = true;
var offline = await new PublishedResultsClient(http, new Uri("https://fixture.invalid/chagi/"), store, () => now).LoadAsync(refresh: true);
Check(offline.IsStale && offline.Notice != null && offline.Competitions.Count == 4, "A new browser session can read a complete saved dataset while offline");
await Fails(() => new PublishedResultsClient(http, new Uri("https://other.invalid/chagi/"), store, () => now).LoadAsync(), "Cached data from a different origin is never reused");
await Fails(() => new PublishedResultsClient(http, new Uri("https://fixture.invalid/chagi/"), new MemorySnapshotStore(), () => now).LoadAsync(), "A first visit offline reports a useful data error");
server.Offline = false;
store.Snapshot!.Files.Remove("results/2026-01-01_fixture.txt");
store.Snapshot.Files["results/2026-02-01_fixture.txt"] = Encoding.UTF8.GetBytes("corrupted cache");
server.Set("results/2026-04-01_fixture.txt", "Fixture 4\n2026-04-01\nTeststad\nMönster Pojkar Kadetter VIT\n1. Test New - Testklubb\n");
before = server.Requests.Count;
await new PublishedResultsClient(http, new Uri("https://fixture.invalid/chagi/"), store, () => now).LoadAsync();
Check(server.Requests.Count == before + 2, "An incomplete local cache repairs only missing or changed files, even within the refresh interval");
server.Files.Remove("results/2026-01-01_fixture.txt"); server.HashBodies.Remove("results/2026-01-01_fixture.txt");
var removed = await source.LoadAsync(refresh: true);
Check(removed.Competitions.Count == 3 && !store.Snapshot!.Files.ContainsKey("results/2026-01-01_fixture.txt"), "Removed manifest files disappear from the committed snapshot");

var original = CompetitorResultsParser.Parse([("2026-04-01_fixture.txt", "Mönster Pojkar Kadetter VIT\n1. Test Alpha - Frölunda")]).Single();
var importedAgain = CompetitorResultsParser.Parse([("2026-04-01_fixture.txt", "Mönster Pojkar Kadetter VIT\n1. Test Alpha - Frölunda")]).Single();
Check(original.Id != importedAgain.Id && ResultLinks.FighterId(original) == ResultLinks.FighterId(importedAgain), "Fighter deep links survive regenerated mobile IDs");
Check(ResultLinks.FighterId(original).StartsWith("test-alpha--frolunda-"), "Fighter analytics paths show name and club");
var previousFighterId = Convert.ToBase64String(Encoding.UTF8.GetBytes(original.FavoriteKey)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
Check(ResultLinks.MatchesFighterId(original, previousFighterId), "Existing fighter deep links remain valid");
var located = CompetitorResultsParser.Parse([("2024-10-19_impact_cup_24.txt", "Impact Cup 24\n2024-10-19 13:00\nPlats: Stockholm\nRegional tävling!\nMönster Pojkar Yngre Miniorer VIT\n1. Test Alpha - Billdal")]).Single();
Check(located.Results.Single().CompetitionCity == "Stockholm", "A labelled place is shown as the competition city");
Check(!ResultLinks.FighterId(original).Any(c => c is '/' or '+' or '='), "Public fighter IDs are safe route segments");
var index = CompetitionMatchParser.ParseIndex("[{\"competitionDate\":\"2026-04-01\",\"competitionName\":\"Fixture\",\"file\":\"fixture.json\"}]");
Check(index.Single().CompetitionDate == new DateTime(2026, 4, 1), "The actual camelCase JSON match contract deserializes");
var matches = CompetitionMatchParser.DeserializeMatches("[{\"matchId\":1,\"redParticipant\":\"Test Alpha - Billdal\",\"redPoints\":\"3.5\"}]");
Check(matches.Single().RedPoints == 3.5m && matches.Single().RedParticipant == "Test Alpha - GBG Kampsport", "Match JSON keeps numeric strings and club normalization");
var lf = Encoding.UTF8.GetBytes("svenska åäö\nresultat\n");
Check(DataIntegrity.MatchesChecksum(lf, new() { Path = "results/test.txt", Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("svenska åäö\r\nresultat\r\n"))) }), "The shared checksum accepts GitHub LF against publisher CRLF hashes");
foreach (var invalid in new[] { "../private.txt", "results/../private.txt", "https://elsewhere/file.txt", "results/file.txt?x=1", "results//file.txt" })
{
    try { DataIntegrity.ValidateManifest(new() { Files = [new() { Path = invalid, Sha256 = new string('0', 64) }] }); throw new Exception("Accepted invalid manifest"); }
    catch (InvalidDataException) { Check(true, "Manifest rejects unsafe path: " + invalid); }
}
using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
try { await source.LoadAsync(token: cancelled.Token); throw new Exception("Cancellation was swallowed"); }
catch (OperationCanceledException) { Check(true, "Cancellation propagates rather than returning stale data"); }
var matchServer = new FixtureServer();
matchServer.Set("results/2026-04-01_fixture.txt", "Fixture\n2026-04-01\nTeststad\nMönster Pojkar Kadetter VIT\n1. Test Alpha - Testklubb\n");
matchServer.Set("matches/index.json", "[{\"competitionDate\":\"2026-04-01\",\"competitionName\":\"Fixture\",\"file\":\"fixture.json\"}]");
matchServer.Set("matches/fixture.json", "[{\"matchId\":1,\"category\":\"Mönster Pojkar Kadetter VIT\",\"redParticipant\":\"Test Extra - Another Club\",\"blueParticipant\":\"Test Alpha - Testklubb\"}]");
var matchData = await new PublishedResultsClient(new HttpClient(matchServer), new Uri("https://fixture.invalid/chagi/"), new MemorySnapshotStore(), () => now).LoadAsync(latestOnly: true);
Check(matchData.Fighters.Single(f => f.LastName == "Extra").Results.Single().Placement == 0, "The web HTTP workflow imports match-only participants with no invented placement");
Check(matchData.Fighters.Single(f => f.LastName == "Alpha").Results.Count == 1 && matchData.Fighters.Single(f => f.LastName == "Alpha").TotalPoints == 5, "Match participants never overwrite or duplicate reported medal placements");
if (args.Length == 2 && args[0] == "--published-dir")
{
    var publishedRoot = Path.GetFullPath(args[1]);
    var manifest = JsonSerializer.Deserialize<ChagiDataManifest>(await File.ReadAllTextAsync(Path.Combine(publishedRoot, "manifest.json")))!;
    var entries = DataIntegrity.ValidateManifest(manifest);
    Check(entries.All(f => DataIntegrity.MatchesChecksum(File.ReadAllBytes(Path.Combine(publishedRoot, f.Path)), f)), "Every file in the local published manifest passes the shared integrity check");
    var textFiles = entries.Where(f => f.Path.StartsWith("results/") && f.Path.EndsWith(".txt"))
        .Select(f => (f.Path, File.ReadAllText(Path.Combine(publishedRoot, f.Path)))).ToList();
    var indexFile = entries.SingleOrDefault(f => f.Path == "matches/index.json");
    var history = indexFile == null ? [] : CompetitionMatchParser.ParseIndex(File.ReadAllText(Path.Combine(publishedRoot, indexFile.Path)))
        .Where(e => e.CompetitionDate.Date <= DateTime.Today).Select(e => new CompetitionMatchHistory(e.CompetitionDate, e.CompetitionName,
            CompetitionMatchParser.DeserializeMatches(File.ReadAllText(Path.Combine(publishedRoot, "matches", e.File))))).ToList();
    var realFighters = CompetitorResultsParser.Parse(textFiles, history);
    var realCompetitions = CompetitionCatalogService.BuildCatalog(realFighters);
    Check(realFighters.Count > 0 && realCompetitions.Count > 0 && realFighters.All(f => f.TotalPoints == f.Results.Sum(r => r.PointsAwarded)), "The actual local published text and match JSON parse with consistent fighter totals");
    Console.WriteLine($"Published snapshot {manifest.Version}: {textFiles.Count} result files, {realCompetitions.Count} competitions, {realFighters.Count} fighters.");
}
Console.WriteLine($"All {count} shared/web data checks passed.");

sealed class MemorySnapshotStore : IDataSnapshotStore
{
    public PublishedSnapshot? Snapshot;
    public Task<PublishedSnapshot?> ReadAsync(CancellationToken token = default) => Task.FromResult(Snapshot);
    public Task WriteAsync(PublishedSnapshot snapshot, CancellationToken token = default) { Snapshot = snapshot; return Task.CompletedTask; }
}
sealed class FixtureServer : HttpMessageHandler
{
    public Dictionary<string, byte[]> Files = new();
    public Dictionary<string, byte[]> HashBodies = new();
    public List<string> Requests = [];
    public bool Offline;
    public string? BadBody;
    public void Set(string path, string text) { Files[path] = Encoding.UTF8.GetBytes(text); HashBodies[path] = Files[path]; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var path = request.RequestUri!.AbsolutePath["/chagi/".Length..];
        Requests.Add(path);
        if (Offline) throw new HttpRequestException("Offline fixture");
        var content = path == "manifest.json" ? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new ChagiDataManifest {
            Version = "fixture", Files = Files.Select(f => new ChagiDataManifestFile { Path = f.Key, Sha256 = Convert.ToHexString(SHA256.HashData(HashBodies[f.Key])) }).ToList()
        })) : path == BadBody ? Encoding.UTF8.GetBytes("corrupted") : Files[path];
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }
}
