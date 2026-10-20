using System.Globalization;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic;

public static class CompetitionCatalogService
{
    public static CompetitionCatalogEntry? FindCurrent(IEnumerable<CompetitionCatalogEntry> catalog, CompetitionCatalogEntry previous)
        => catalog.FirstOrDefault(entry => entry.Key == previous.Key)
            // Venue information can be corrected or added by a later result file.
            ?? catalog.FirstOrDefault(entry => entry.CompetitionDate.Date == previous.CompetitionDate.Date &&
                string.Equals(entry.CompetitionName.Trim(), previous.CompetitionName.Trim(), StringComparison.OrdinalIgnoreCase));


    public static List<CompetitionCatalogEntry> BuildCatalog(IEnumerable<Competitor> competitors)
    {
        var flattenedResults = competitors
            .SelectMany(competitor => competitor.Results.Select(result => new CompetitionCatalogResult
            {
                CompetitionDate = result.CompetitionDate,
                CompetitionName = result.CompetitionName,
                CompetitionCity = result.CompetitionCity,
                Discipline = result.Discipline,
                BeltGroup = result.BeltGroup,
                CategoryClass = result.CategoryClass,
                Division = result.Division,
                Placement = result.Placement,
                CompetitorId = competitor.Id,
                CompetitorName = competitor.FullName,
                CompetitorLastName = competitor.LastName,
                Team = competitor.Team
            }))
            .ToList();

        return flattenedResults
            .GroupBy(BuildCompetitionKey)
            .Select(group => BuildCompetitionEntry(group.Key, group.ToList()))
            .OrderByDescending(entry => entry.CompetitionDate)
            .ThenBy(entry => entry.CompetitionName, SwedishNameComparer.Instance)
            .ToList();
    }

    static CompetitionCatalogEntry BuildCompetitionEntry(string key, List<CompetitionCatalogResult> results)
    {
        var first = results[0];

        return new CompetitionCatalogEntry
        {
            Key = key,
            CompetitionDate = first.CompetitionDate,
            CompetitionName = first.CompetitionName,
            CompetitionCity = first.CompetitionCity,
            DisciplineCounts = results
                .GroupBy(result => result.Discipline)
                .Select(group => new CompetitionDisciplineCount
                {
                    Discipline = group.Key,
                    Count = group
                        .GroupBy(BuildDivisionKey)
                        .Count()
                })
                .OrderBy(group => GetDisciplineOrder(group.Discipline))
                .ToList(),
            Divisions = results
                .GroupBy(BuildDivisionKey)
                .Select(group => BuildDivisionEntry(group.ToList()))
                .OrderBy(entry => GetDisciplineOrder(entry.Discipline))
                .ThenBy(entry => entry.CategoryClass, SwedishNameComparer.Instance)
                .ThenBy(entry => entry.BeltGroup, SwedishNameComparer.Instance)
                .ThenBy(entry => entry.Division, SwedishNameComparer.Instance)
                .ToList()
        };
    }

    static CompetitionDivisionEntry BuildDivisionEntry(List<CompetitionCatalogResult> results)
    {
        var first = results[0];

        return new CompetitionDivisionEntry
        {
            Discipline = first.Discipline,
            BeltGroup = first.BeltGroup,
            CategoryClass = first.CategoryClass,
            Division = first.Division,
            IsMale = ParseMaleDivision(first.Division),
            AgeClass = ParseAgeClass(first.Division),
            Results = results
                .OrderBy(result => result.Placement <= 0 ? int.MaxValue : result.Placement)
                .ThenBy(result => result.CompetitorName, SwedishNameComparer.Instance)
                .Select(result => new CompetitionPlacementEntry
                {
                    Placement = result.Placement,
                    CompetitorId = result.CompetitorId,
                    CompetitorName = result.CompetitorName,
                    CompetitorLastName = result.CompetitorLastName,
                    Team = result.Team
                })
                .ToList()
        };
    }

    static string BuildCompetitionKey(CompetitionCatalogResult result)
    {
        return string.Join("|",
            result.CompetitionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            result.CompetitionName,
            result.CompetitionCity);
    }

    static string BuildDivisionKey(CompetitionCatalogResult result)
    {
        return string.Join("|",
            BuildCompetitionKey(result),
            result.Discipline,
            result.CategoryClass,
            result.BeltGroup,
            result.Division);
    }

    static int GetDisciplineOrder(CompetitionDisciplineE discipline)
    {
        return discipline switch
        {
            CompetitionDisciplineE.Tul => 0,
            CompetitionDisciplineE.Sparring => 1,
            CompetitionDisciplineE.Specialtekniker => 2,
            CompetitionDisciplineE.Kross => 3,
            _ => 99
        };
    }

    static bool? ParseMaleDivision(string division)
    {
        if (division.Contains("Flickor/Pojkar", StringComparison.OrdinalIgnoreCase))
            return null;

        if (division.Contains("Pojkar", StringComparison.OrdinalIgnoreCase) ||
            division.Contains("Herrar", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (division.Contains("Flickor", StringComparison.OrdinalIgnoreCase) ||
            division.Contains("Damer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    static RankingAgeClassE? ParseAgeClass(string division)
    {
        if (division.Contains("Yngre Miniorer", StringComparison.OrdinalIgnoreCase))
            return RankingAgeClassE.YngreMiniorer;

        if (division.Contains("Miniorer", StringComparison.OrdinalIgnoreCase) &&
            !division.Contains("Yngre Miniorer", StringComparison.OrdinalIgnoreCase))
        {
            return RankingAgeClassE.Miniorer;
        }

        if (division.Contains("Kadetter", StringComparison.OrdinalIgnoreCase))
            return RankingAgeClassE.Kadetter;

        if (division.Contains("Yngre Juniorer", StringComparison.OrdinalIgnoreCase))
            return RankingAgeClassE.YngreJuniorer;

        if (division.Contains("Juniorer", StringComparison.OrdinalIgnoreCase) &&
            !division.Contains("Yngre Juniorer", StringComparison.OrdinalIgnoreCase))
        {
            return RankingAgeClassE.Juniorer;
        }

        return null;
    }
}

public sealed class CompetitionCatalogEntry
{
    // Complete participant lists are available from Torslandacupen 2026 onward.
    static readonly DateTime CompleteParticipantListsFrom = new(2026, 9, 26);

    public string Key { get; set; } = string.Empty;
    public DateTime CompetitionDate { get; set; }
    public string CompetitionName { get; set; } = string.Empty;
    public string CompetitionCity { get; set; } = string.Empty;
    public List<CompetitionDisciplineCount> DisciplineCounts { get; set; } = new();
    public List<CompetitionDivisionEntry> Divisions { get; set; } = new();

    public int? ParticipantCount => CompetitionDate.Date >= CompleteParticipantListsFrom
        ? Divisions.SelectMany(division => division.Results).Select(result => result.CompetitorId).Distinct().Count()
        : null;
}

public sealed class CompetitionDisciplineCount
{
    public CompetitionDisciplineE Discipline { get; set; }
    public int Count { get; set; }
}

public sealed class CompetitionDivisionEntry : System.ComponentModel.INotifyPropertyChanged
{
    bool _hasMatchData;
    public bool HasMatchData
    {
        get => _hasMatchData;
        set
        {
            if (_hasMatchData == value) return;
            _hasMatchData = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasMatchData)));
        }
    }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public CompetitionDisciplineE Discipline { get; set; }
    public string BeltGroup { get; set; } = string.Empty;
    public string CategoryClass { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public bool? IsMale { get; set; }
    public RankingAgeClassE? AgeClass { get; set; }
    // Preserve the user's expansion when CollectionView recycles this division's card.
    public bool IsOtherParticipantsExpanded { get; set; }
    public List<CompetitionPlacementEntry> Results { get; set; } = new();
}

public sealed class CompetitionPlacementEntry
{
    public int Placement { get; set; }
    public Guid CompetitorId { get; set; }
    public string CompetitorName { get; set; } = string.Empty;
    public string CompetitorLastName { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
}

sealed class CompetitionCatalogResult
{
    public DateTime CompetitionDate { get; set; }
    public string CompetitionName { get; set; } = string.Empty;
    public string CompetitionCity { get; set; } = string.Empty;
    public CompetitionDisciplineE Discipline { get; set; }
    public string BeltGroup { get; set; } = string.Empty;
    public string CategoryClass { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public int Placement { get; set; }
    public Guid CompetitorId { get; set; }
    public string CompetitorName { get; set; } = string.Empty;
    public string CompetitorLastName { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
}
