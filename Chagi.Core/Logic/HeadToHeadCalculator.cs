using System.Text;
using System.Globalization;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class HeadToHeadCalculator
{
    static readonly StringComparer NameComparer = StringComparer.Create(CultureInfo.GetCultureInfo("sv-SE"), ignoreCase: true);

    public static IReadOnlyList<HeadToHeadSection> Build(Competitor fighter,
        IEnumerable<CompetitionMatchHistory> history, IEnumerable<Competitor> competitors)
    {
        var identity = Identity(fighter.FullName, fighter.Team);
        var sections = new Dictionary<CompetitionDisciplineE, Dictionary<string, HeadToHeadOpponent>>();
        var results = competitors.SelectMany(c => c.Results.Select(r => new
            { Identity = Identity(c.FullName, c.Team), Participant = $"{c.FullName} - {c.Team}", Result = r })).ToList();
        var placements = results
            .ToLookup(item => PlacementKey(item.Identity, item.Result.CompetitionDate, item.Result.CompetitionName, item.Result.Division));
        var matchCategories = new HashSet<string>();

        foreach (var competition in history.Where(c => c.Date.Date <= DateTime.Today)
                     .DistinctBy(c => $"{c.Date:yyyy-MM-dd}|{Normalize(c.Name)}"))
        {
            // A category with match data must never also generate inferred podium meetings,
            // even when its matches are unfinished or do not involve this fighter.
            foreach (var match in competition.Matches)
                matchCategories.Add(CategoryKey(competition.Date, competition.Name, match.Category));
            var matches = competition.Matches.Where(m => m.Finished == 1).DistinctBy(m => m.MatchId).ToList();
            foreach (var match in matches.Where(m => m.RedId.HasValue && m.HasBlueParticipant
                         && !string.IsNullOrWhiteSpace(m.RedParticipant) && m.RedParticipant != "?"
                         && m.Discipline is "PATTERN" or "SPARRING"))
            {
                var isRed = ParticipantIdentity(match.RedParticipant) == identity;
                var isBlue = ParticipantIdentity(match.BlueParticipant) == identity;
                if (isRed == isBlue) continue;
                var opponent = isRed ? match.BlueParticipant! : match.RedParticipant!;
                var fighterPoints = isRed ? match.RedPoints : match.BluePoints;
                var opponentPoints = isRed ? match.BluePoints : match.RedPoints;
                var fighterId = isRed ? match.RedId : match.BlueId;
                var opponentId = isRed ? match.BlueId : match.RedId;
                var outcome = match.WinnerId == fighterId && fighterId.HasValue ? HeadToHeadOutcome.Win
                    : match.WinnerId == opponentId && opponentId.HasValue ? HeadToHeadOutcome.Loss
                    : Compare(fighterPoints, opponentPoints, lowerWins: false);
                Add(match.Discipline == "PATTERN" ? CompetitionDisciplineE.Tul : CompetitionDisciplineE.Sparring,
                    opponent, new(competition.Date, competition.Name, match.Category, outcome, false,
                        fighterPoints, opponentPoints, isRed ? match.RedFlags : match.BlueFlags,
                        isRed ? match.BlueFlags : match.RedFlags, null, null));
            }

            // Spec and power breaking compare one final placement per shared category, never match points.
            foreach (var category in matches.Where(m => m.IsIndividualAttempt).GroupBy(m => Normalize(m.Category)))
            {
                var first = category.First();
                var participants = category.SelectMany(m => new[] { m.RedParticipant, m.BlueParticipant })
                    .Where(p => !string.IsNullOrWhiteSpace(p) && p != "?" && p.Contains(" - "))
                    .Select(p => p!).DistinctBy(ParticipantIdentity).ToList();
                if (!participants.Any(p => ParticipantIdentity(p) == identity)) continue;
                var fighterPlacement = GetPlacement(identity, competition, first.Category);
                foreach (var opponent in participants.Where(p => ParticipantIdentity(p) != identity))
                {
                    var opponentPlacement = GetPlacement(ParticipantIdentity(opponent), competition, first.Category);
                    Add(first.Discipline == "SPEC_TECH" ? CompetitionDisciplineE.Specialtekniker : CompetitionDisciplineE.Kross,
                        opponent, new(competition.Date, competition.Name, first.Category,
                            ComparePlacements(fighterPlacement, opponentPlacement), true,
                            null, null, null, null, fighterPlacement, opponentPlacement));
                }
            }
        }

        // Medal-only sources establish an order only between distinct podium positions
        // in the same individual category. Shared bronzes do not establish a meeting.
        foreach (var category in results.Where(p => p.Result.CompetitionDate.Date <= DateTime.Today
                     && p.Result.Placement is >= 1 and <= 3
                     && !string.IsNullOrWhiteSpace(p.Result.Division)
                     && !p.Result.Division.TrimStart().StartsWith("Lag", StringComparison.OrdinalIgnoreCase))
                 .GroupBy(p => (Category: CategoryKey(p.Result.CompetitionDate, p.Result.CompetitionName, p.Result.Division),
                     p.Result.Discipline)))
        {
            if (matchCategories.Contains(category.Key.Category)) continue;
            var participants = category.GroupBy(p => p.Identity)
                .Where(g => g.Select(p => p.Result.Placement).Distinct().Count() == 1)
                .Select(g => g.First()).ToList();
            var own = participants.SingleOrDefault(p => p.Identity == identity);
            if (own == null) continue;
            foreach (var opponent in participants.Where(p => p.Identity != identity && p.Result.Placement != own.Result.Placement))
            {
                var result = own.Result;
                Add(result.Discipline, opponent.Participant,
                    new(result.CompetitionDate, result.CompetitionName, result.Division,
                        ComparePlacements(result.Placement, opponent.Result.Placement), true,
                        null, null, null, null, result.Placement, opponent.Result.Placement));
            }
        }

        return new[] { CompetitionDisciplineE.Tul, CompetitionDisciplineE.Kross, CompetitionDisciplineE.Sparring, CompetitionDisciplineE.Specialtekniker }
            .Select(discipline => new HeadToHeadSection(discipline,
                sections.TryGetValue(discipline, out var opponents)
                    ? SortOpponents(opponents.Values)
                    : []))
            .ToList();

        int? GetPlacement(string participant, CompetitionMatchHistory competition, string division)
        {
            var known = placements[PlacementKey(participant, competition.Date, competition.Name, division)]
                .Select(p => p.Result.Placement).Where(p => p > 0).Distinct().ToList();
            return known.Count == 1 ? known[0] : null;
        }

        void Add(CompetitionDisciplineE discipline, string opponentIdentity, HeadToHeadMeeting meeting)
        {
            if (!sections.TryGetValue(discipline, out var opponents))
                sections[discipline] = opponents = new();
            var key = ParticipantIdentity(opponentIdentity);
            if (!opponents.TryGetValue(key, out var opponent))
            {
                var parts = ClubCatalogService.NormalizeParticipant(opponentIdentity)!.Split(" - ", 2);
                opponents[key] = opponent = new() { Name = parts[0].Trim(), Team = parts.Length > 1 ? parts[1].Trim() : string.Empty };
            }
            opponent.Meetings.Add(meeting with { Discipline = discipline });
        }
    }

    public static IReadOnlyList<HeadToHeadOpponent> GroupByOpponent(IEnumerable<HeadToHeadSection> sections)
    {
        return SortOpponents(sections.SelectMany(section => section.Opponents)
            .GroupBy(opponent => Identity(opponent.Name, opponent.Team))
            .Select(group =>
            {
                var first = group.First();
                var opponent = new HeadToHeadOpponent { Name = first.Name, Team = first.Team };
                opponent.Meetings.AddRange(group.SelectMany(o => o.Meetings));
                return opponent;
            }));
    }

    static List<HeadToHeadOpponent> SortOpponents(IEnumerable<HeadToHeadOpponent> opponents) =>
        opponents.OrderBy(o => o.Name, NameComparer).ThenBy(o => o.Team, NameComparer).ToList();

    static HeadToHeadOutcome ComparePlacements(int? own, int? other)
    {
        // Only medal positions establish an order; all non-medallists draw with each other.
        var ownRank = own is >= 1 and <= 3 ? own.Value : 4;
        var otherRank = other is >= 1 and <= 3 ? other.Value : 4;
        return Compare(ownRank, otherRank, lowerWins: true);
    }

    static HeadToHeadOutcome Compare(decimal? own, decimal? other, bool lowerWins)
    {
        if (!own.HasValue || !other.HasValue) return HeadToHeadOutcome.Unknown;
        if (own == other) return HeadToHeadOutcome.Draw;
        return (lowerWins ? own < other : own > other) ? HeadToHeadOutcome.Win : HeadToHeadOutcome.Loss;
    }

    static string Normalize(string? text) => (text ?? string.Empty).Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    static string ParticipantIdentity(string? participant) => Normalize(ClubCatalogService.NormalizeParticipant(participant));
    static string Identity(string name, string team) => ParticipantIdentity($"{name.Trim()} - {team.Trim()}");
    static string CategoryKey(DateTime date, string competition, string division) =>
        $"{date:yyyy-MM-dd}|{Normalize(competition)}|{Normalize(division)}";
    static string PlacementKey(string identity, DateTime date, string competition, string division) =>
        $"{identity}|{date:yyyy-MM-dd}|{Normalize(competition)}|{Normalize(division)}";
}
