using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ITF_Taekwondo_Gradering.Models;
using ITF_Taekwondo_Gradering.Models.Ranking;

namespace ITF_Taekwondo_Gradering.Logic
{
    // Extracted from the mobile repository; both clients execute this implementation.
    public static class CompetitorResultsParser
    {
        public static List<Competitor> Parse(
            IEnumerable<(string fileName, string text)> sourceFiles,
            IReadOnlyList<CompetitionMatchHistory>? history = null)
        {
            var matchHistory = history ?? Array.Empty<CompetitionMatchHistory>();
                var parsedCompetitors = new List<Competitor>();

                List<(string fileName, string text)> filesToParse = new();
                var seenSourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var (fileName, text) in sourceFiles)
                {
                    AddSourceFile(filesToParse, seenSourceFiles, fileName, text);
                }
                var byIdentity = new Dictionary<string, Competitor>(StringComparer.OrdinalIgnoreCase);
                var latestCompetitionByCompetitor = new Dictionary<Guid, DateTime>();
                var latestCompetitionWithAgeClassByCompetitor = new Dictionary<Guid, DateTime>();
                var resultLineRegex = new Regex(
                    @"^\s*(?<placement>\d+)\.\s+(?<name>.+?)\s+-\s+(?<team>.+?)\s*$",
                    RegexOptions.Compiled);

                foreach (var (fileName, text) in filesToParse)
                {
                    var competitionInfo = ParseCompetitionInfo(fileName, text);
                    var competitionDate = competitionInfo.Date;

                    var currentDivision = string.Empty;
                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.Trim();
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (!char.IsDigit(line[0]))
                        {
                            currentDivision = line;
                            continue;
                        }

                        var match = resultLineRegex.Match(line);
                        if (!match.Success)
                            continue;

                        var full = match.Groups["name"].Value.Trim();
                        var team = ClubCatalogService.NormalizeClubName(match.Groups["team"].Value.Trim());
                        var placement = int.TryParse(match.Groups["placement"].Value, out var parsedPlacement)
                            ? parsedPlacement
                            : 0;
                        var discipline = ParseDiscipline(currentDivision);
                        var pointsAwarded = CalculatePoints(placement);
                        var ratingAwarded = pointsAwarded;
                        var parsedGender = ParseMaleDivision(currentDivision);
                        var divisionMetadata = ParseDivisionMetadata(currentDivision);

                        // Names are not unique across clubs. Keep each participant's results separate.
                        var identity = $"{full}|{team}";
                        if (!byIdentity.TryGetValue(identity, out var comp))
                        {
                            var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var first = parts.Length > 0 ? parts[0] : string.Empty;
                            var last = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;
                            comp = new Competitor
                            {
                                FirstName = first,
                                LastName = last,
                                Team = team,
                                IsMale = parsedGender,
                            };
                            byIdentity[identity] = comp;
                            parsedCompetitors.Add(comp);
                        }
                        else if (string.IsNullOrWhiteSpace(comp.Team) && !string.IsNullOrWhiteSpace(team))
                        {
                            comp.Team = team;
                        }

                        if (parsedGender.HasValue)
                        {
                            comp.IsMale = parsedGender.Value;
                        }

                        if (!latestCompetitionByCompetitor.TryGetValue(comp.Id, out var latestCompetitionDate) ||
                            competitionDate >= latestCompetitionDate)
                        {
                            latestCompetitionByCompetitor[comp.Id] = competitionDate;
                        }

                        UpdateCurrentAgeClass(comp, currentDivision);
                        UpdateLatestCompetitionAgeClass(comp, currentDivision, competitionDate, latestCompetitionWithAgeClassByCompetitor);

                        AddScore(comp, discipline, pointsAwarded, ratingAwarded);

                        var result = new CompetitionResult
                        {
                            CompetitorId = comp.Id,
                            CompetitionId = Guid.Empty,
                            CompetitionDate = competitionDate == DateTime.MinValue ? DateTime.UtcNow : competitionDate,
                            CompetitionName = competitionInfo.Name,
                            CompetitionCity = competitionInfo.City,
                            Division = currentDivision,
                            BeltGroup = divisionMetadata.BeltGroup,
                            CategoryClass = divisionMetadata.CategoryClass,
                            Placement = placement,
                            PointsAwarded = pointsAwarded,
                            RatingAwarded = ratingAwarded,
                            Discipline = discipline
                        };

                        comp.Results.Add(result);
                    }
                }

                var competitionInfos = filesToParse.Select(file => ParseCompetitionInfo(file.fileName, file.text)).ToList();
                foreach (var competition in matchHistory)
                foreach (var match in competition.Matches)
                foreach (var participant in new[] { match.RedParticipant, match.BlueParticipant })
                {
                    if (string.IsNullOrWhiteSpace(match.Category) || string.IsNullOrWhiteSpace(participant))
                        continue;
                    var participantParts = participant.Split(" - ", 2, StringSplitOptions.TrimEntries);
                    if (participantParts.Length != 2 || string.IsNullOrWhiteSpace(participantParts[0]) ||
                        participantParts[0] == "?" || string.IsNullOrWhiteSpace(participantParts[1]))
                        continue;
                    var fullName = participantParts[0];
                    var team = ClubCatalogService.NormalizeClubName(participantParts[1]);
                    var identity = $"{fullName}|{team}";
                    if (!byIdentity.TryGetValue(identity, out var competitor))
                    {
                        var names = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        competitor = new Competitor
                        {
                            FirstName = names[0], LastName = string.Join(' ', names.Skip(1)), Team = team,
                            IsMale = ParseMaleDivision(match.Category)
                        };
                        byIdentity[identity] = competitor;
                        parsedCompetitors.Add(competitor);
                    }
                    // Keep reported placements; repeated matches represent one category entry.
                    if (competitor.Results.Any(r => r.CompetitionDate.Date == competition.Date.Date &&
                        string.Equals(r.CompetitionName.Trim(), competition.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.Division.Trim(), match.Category.Trim(), StringComparison.OrdinalIgnoreCase)))
                        continue;
                    if (!latestCompetitionByCompetitor.TryGetValue(competitor.Id, out var latestDate) || competition.Date >= latestDate)
                    {
                        competitor.IsMale = ParseMaleDivision(match.Category) ?? competitor.IsMale;
                        latestCompetitionByCompetitor[competitor.Id] = competition.Date;
                    }
                    UpdateCurrentAgeClass(competitor, match.Category);
                    UpdateLatestCompetitionAgeClass(competitor, match.Category, competition.Date, latestCompetitionWithAgeClassByCompetitor);
                    var metadata = ParseDivisionMetadata(match.Category);
                    competitor.Results.Add(new CompetitionResult
                    {
                        CompetitorId = competitor.Id, CompetitionDate = competition.Date,
                        CompetitionName = competition.Name, Division = match.Category.Trim(),
                        CompetitionCity = competitionInfos.FirstOrDefault(info => info.Date.Date == competition.Date.Date &&
                            string.Equals(info.Name.Trim(), competition.Name.Trim(), StringComparison.OrdinalIgnoreCase))?.City ?? string.Empty,
                        Discipline = ParseDiscipline(match.Category), BeltGroup = metadata.BeltGroup,
                        CategoryClass = metadata.CategoryClass, Placement = 0
                    });
                }

                foreach (var competitor in parsedCompetitors)
                    competitor.LatestBelt = ResolveLatestBelt(competitor.Results);


            RebuildRankingSnapshots(parsedCompetitors);
            return parsedCompetitors;
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

        static CompetitionDisciplineE ParseDiscipline(string division)
        {
            if (division.StartsWith("Sparring", StringComparison.OrdinalIgnoreCase))
                return CompetitionDisciplineE.Sparring;

            if (division.StartsWith("Spec.", StringComparison.OrdinalIgnoreCase))
                return CompetitionDisciplineE.Specialtekniker;

            if (division.StartsWith("Kross", StringComparison.OrdinalIgnoreCase))
                return CompetitionDisciplineE.Kross;

            return CompetitionDisciplineE.Tul;
        }

        static int CalculatePoints(int placement)
        {
            return placement switch
            {
                1 => 5,
                2 => 3,
                3 => 1,
                _ => 0,
            };
        }

        static void AddScore(Competitor competitor, CompetitionDisciplineE discipline, int points, decimal rating)
        {
            var summary = discipline switch
            {
                CompetitionDisciplineE.Tul => competitor.Tul,
                CompetitionDisciplineE.Sparring => competitor.Sparring,
                CompetitionDisciplineE.Specialtekniker => competitor.Specialtekniker,
                CompetitionDisciplineE.Kross => competitor.Kross,
                _ => competitor.Tul,
            };

            summary.Points += points;
            summary.Rating += rating;
        }

        static void UpdateCurrentAgeClass(Competitor competitor, string division)
        {
            var parsedAgeClass = ParseAgeClass(division);
            if (parsedAgeClass == null)
                return;

            if (competitor.CurrentAgeClass == null || parsedAgeClass > competitor.CurrentAgeClass)
            {
                competitor.CurrentAgeClass = parsedAgeClass;
            }
        }

        static void UpdateLatestCompetitionAgeClass(
            Competitor competitor,
            string division,
            DateTime competitionDate,
            Dictionary<Guid, DateTime> latestCompetitionWithAgeClassByCompetitor)
        {
            var parsedAgeClass = ParseAgeClass(division);
            if (!parsedAgeClass.HasValue)
                return;

            if (!latestCompetitionWithAgeClassByCompetitor.TryGetValue(competitor.Id, out var latestCompetitionDate) ||
                competitionDate >= latestCompetitionDate)
            {
                competitor.LatestCompetitionAgeClass = parsedAgeClass;
                latestCompetitionWithAgeClassByCompetitor[competitor.Id] = competitionDate;
            }
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

        static ChildBelt ResolveLatestBelt(IEnumerable<CompetitionResult> results)
        {
            // A combined class permits several belts; it does not establish the highest one.
            // On the latest competition date, prefer the most specific reported belt class.
            return results
                .Where(result => !string.IsNullOrWhiteSpace(result.BeltGroup))
                .Select(result => new
                {
                    Date = result.CompetitionDate.Date,
                    Belt = ParseLatestBelt(result.Division),
                    ColorCount = result.BeltGroup.Split('(')[0]
                        .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length
                })
                .OrderByDescending(result => result.Date)
                .ThenBy(result => result.ColorCount)
                // Conflicting equally specific classes must not depend on file or row order.
                .ThenBy(result => result.Belt.Level)
                .Select(result => result.Belt)
                .FirstOrDefault() ?? ChildBelt.Kup10_1;
        }

        static ChildBelt ParseLatestBelt(string division)
        {
            if (TryParseDanBeltGroup(division, out _) || ContainsStandaloneBlackBelt(division))
            {
                return ChildBelt.Kup5_2;
            }

            if (TryParseKupRank(division, out var kupRank))
            {
                return MapKupRankToLatestBelt(kupRank);
            }

            if (division.Contains("GRÖN", StringComparison.OrdinalIgnoreCase) ||
                division.Contains("BLÅ", StringComparison.OrdinalIgnoreCase) ||
                division.Contains("RÖD", StringComparison.OrdinalIgnoreCase))
            {
                return ChildBelt.Kup6_1;
            }

            if (division.Contains("GUL", StringComparison.OrdinalIgnoreCase))
                return ChildBelt.Kup8_1;

            return ChildBelt.Kup10_1;
        }

        static (string BeltGroup, string CategoryClass) ParseDivisionMetadata(string division)
        {
            if (string.IsNullOrWhiteSpace(division))
                return (string.Empty, string.Empty);

            var beltGroup = string.Empty;
            var categoryClass = string.Empty;

            var categoryClassMatch = Regex.Match(division, @"(?<class>[+-]?\d+(?:cm|kg)(?:/[+-]?\d+(?:cm|kg))*)(?:\s*\([^)]+\))?\s*$", RegexOptions.IgnoreCase);
            if (categoryClassMatch.Success)
            {
                categoryClass = categoryClassMatch.Groups["class"].Value.Trim();
            }

            if (TryParseDanBeltGroup(division, out var danBeltGroup))
            {
                beltGroup = danBeltGroup;
            }
            else if (TryParseKupBeltGroup(division, out var kupBeltGroup))
            {
                beltGroup = kupBeltGroup;
            }
            else
            {
                var beltGroupMatch = Regex.Match(
                    division,
                    @"\b(?<belt>VIT|GUL|GRÖN|BLÅ|RÖD|SVART)(?:/(?<belt2>VIT|GUL|GRÖN|BLÅ|RÖD|SVART))*\b",
                    RegexOptions.IgnoreCase);

                if (beltGroupMatch.Success)
                {
                    beltGroup = CanonicalizeBeltGroup(beltGroupMatch.Value);
                }
            }

            return (beltGroup, categoryClass);
        }

        static bool TryParseDanBeltGroup(string division, out string beltGroup)
        {
            beltGroup = string.Empty;
            if (string.IsNullOrWhiteSpace(division))
                return false;

            var rangeMatch = Regex.Match(division, @"\b(?<from>[1-9])\s*-\s*(?<to>[1-9])\s*Dan\b", RegexOptions.IgnoreCase);
            if (rangeMatch.Success)
            {
                beltGroup = $"{rangeMatch.Groups["from"].Value}-{rangeMatch.Groups["to"].Value} Dan";
                return true;
            }

            var danMatch = Regex.Match(division, @"\b(?<dan>[1-9])\s*Dan\b", RegexOptions.IgnoreCase);
            if (danMatch.Success)
            {
                beltGroup = $"{danMatch.Groups["dan"].Value} Dan";
                return true;
            }

            if (ContainsStandaloneBlackBelt(division))
            {
                beltGroup = "Dan 6";
                return true;
            }

            return false;
        }

        static bool ContainsStandaloneBlackBelt(string division)
        {
            return Regex.IsMatch(division, @"(?<!/)\bSVART\b(?!/)", RegexOptions.IgnoreCase);
        }

        static bool TryParseKupRank(string division, out int kupRank)
        {
            kupRank = 0;
            if (string.IsNullOrWhiteSpace(division))
                return false;

            var rangeMatch = Regex.Match(
                division,
                @"\b(?<from>10|[1-9])\s*-\s*(?<to>10|[1-9])\s*Kup\b",
                RegexOptions.IgnoreCase);
            if (rangeMatch.Success &&
                int.TryParse(rangeMatch.Groups["from"].Value, out var fromKup) &&
                int.TryParse(rangeMatch.Groups["to"].Value, out var toKup))
            {
                kupRank = Math.Min(fromKup, toKup);
                return true;
            }

            var kupMatch = Regex.Match(division, @"\b(?<kup>10|[1-9])\s*Kup\b", RegexOptions.IgnoreCase);
            if (kupMatch.Success && int.TryParse(kupMatch.Groups["kup"].Value, out var singleKup))
            {
                kupRank = singleKup;
                return true;
            }

            return false;
        }

        static bool TryParseKupBeltGroup(string division, out string beltGroup)
        {
            beltGroup = string.Empty;
            if (string.IsNullOrWhiteSpace(division))
                return false;

            var rangeMatch = Regex.Match(
                division,
                @"\b(?<from>10|[1-9])\s*-\s*(?<to>10|[1-9])\s*Kup\b",
                RegexOptions.IgnoreCase);
            if (rangeMatch.Success &&
                int.TryParse(rangeMatch.Groups["from"].Value, out var fromKup) &&
                int.TryParse(rangeMatch.Groups["to"].Value, out var toKup))
            {
                var fromColor = ResolveKupColorLabel(fromKup);
                var toColor = ResolveKupColorLabel(toKup);
                if (string.IsNullOrWhiteSpace(fromColor) || string.IsNullOrWhiteSpace(toColor))
                    return false;

                beltGroup = string.Equals(fromColor, toColor, StringComparison.Ordinal)
                    ? $"{fromColor} ({fromKup}-{toKup} Kup)"
                    : $"{fromColor}/{toColor} ({fromKup}-{toKup} Kup)";
                return true;
            }

            var kupMatch = Regex.Match(division, @"\b(?<kup>10|[1-9])\s*Kup\b", RegexOptions.IgnoreCase);
            if (kupMatch.Success && int.TryParse(kupMatch.Groups["kup"].Value, out var singleKup))
            {
                var color = ResolveKupColorLabel(singleKup);
                if (string.IsNullOrWhiteSpace(color))
                    return false;

                beltGroup = $"{color} ({singleKup} Kup)";
                return true;
            }

            return false;
        }

        static ChildBelt MapKupRankToLatestBelt(int kupRank)
        {
            return kupRank switch
            {
                >= 9 => ChildBelt.Kup10_1,
                >= 7 => ChildBelt.Kup8_1,
                >= 1 => ChildBelt.Kup6_1,
                _ => ChildBelt.Kup10_1
            };
        }

        static string ResolveKupColorLabel(int kupRank)
        {
            return kupRank switch
            {
                10 or 9 => "VIT",
                8 or 7 => "GUL",
                6 or 5 => "GRÖN",
                4 or 3 => "BLÅ",
                2 or 1 => "RÖD",
                _ => string.Empty
            };
        }

        static string CanonicalizeBeltGroup(string rawBeltGroup)
        {
            if (string.IsNullOrWhiteSpace(rawBeltGroup))
                return string.Empty;

            var segments = rawBeltGroup
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(CanonicalizeBeltSegment);

            return string.Join("/", segments);
        }

        static string CanonicalizeBeltSegment(string segment)
        {
            var upper = segment.Trim().ToUpperInvariant();
            return upper switch
            {
                "VIT" => "VIT",
                "GUL" => "GUL",
                "GRÖN" => "GRÖN",
                "BLÅ" => "BLÅ",
                "RÖD" => "RÖD",
                "SVART" => "SVART",
                _ => segment.Trim()
            };
        }

        static void AddSourceFile(
            List<(string fileName, string text)> filesToParse,
            HashSet<string> seenSourceFiles,
            string fileName,
            string text)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            if (!seenSourceFiles.Add(fileName.Trim()))
                return;

            filesToParse.Add((fileName, text));
        }

        public static bool NormalizeCache(List<Competitor> competitors)
        {
            var changed = false;

            foreach (var competitor in competitors)
            {
                var distinctResults = competitor.Results
                    .GroupBy(BuildResultFingerprint, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(r => r.CompetitionDate)
                    .ThenBy(r => r.CompetitionName)
                    .ThenBy(r => r.Division)
                    .ThenBy(r => r.Discipline)
                    .ThenBy(r => r.Placement)
                    .ToList();

                if (distinctResults.Count != competitor.Results.Count)
                {
                    competitor.Results = distinctResults;
                    changed = true;
                }

                var rebuiltTul = new DisciplineScoreSummary();
                var rebuiltSparring = new DisciplineScoreSummary();
                var rebuiltSpecialtekniker = new DisciplineScoreSummary();
                var rebuiltKross = new DisciplineScoreSummary();
                RankingAgeClassE? rebuiltAgeClass = null;
                RankingAgeClassE? rebuiltLatestCompetitionAgeClass = null;
                var latestCompetitionDateWithAgeClass = DateTime.MinValue;

                foreach (var result in competitor.Results)
                {
                    result.CompetitorId = competitor.Id;
                    var divisionMetadata = ParseDivisionMetadata(result.Division);
                    if (!string.Equals(result.BeltGroup, divisionMetadata.BeltGroup, StringComparison.Ordinal))
                    {
                        result.BeltGroup = divisionMetadata.BeltGroup;
                        changed = true;
                    }

                    if (!string.Equals(result.CategoryClass, divisionMetadata.CategoryClass, StringComparison.Ordinal))
                    {
                        result.CategoryClass = divisionMetadata.CategoryClass;
                        changed = true;
                    }

                    var summary = result.Discipline switch
                    {
                        CompetitionDisciplineE.Tul => rebuiltTul,
                        CompetitionDisciplineE.Sparring => rebuiltSparring,
                        CompetitionDisciplineE.Specialtekniker => rebuiltSpecialtekniker,
                        CompetitionDisciplineE.Kross => rebuiltKross,
                        _ => rebuiltTul,
                    };

                    summary.Points += result.PointsAwarded;
                    summary.Rating += result.RatingAwarded;

                    var parsedAgeClass = ParseAgeClass(result.Division);
                    if (parsedAgeClass.HasValue &&
                        (!rebuiltAgeClass.HasValue || parsedAgeClass > rebuiltAgeClass))
                    {
                        rebuiltAgeClass = parsedAgeClass;
                    }

                    if (parsedAgeClass.HasValue && result.CompetitionDate >= latestCompetitionDateWithAgeClass)
                    {
                        rebuiltLatestCompetitionAgeClass = parsedAgeClass;
                        latestCompetitionDateWithAgeClass = result.CompetitionDate;
                    }
                }

                if (competitor.Tul.Points != rebuiltTul.Points || competitor.Tul.Rating != rebuiltTul.Rating)
                {
                    competitor.Tul = rebuiltTul;
                    changed = true;
                }

                if (competitor.Sparring.Points != rebuiltSparring.Points || competitor.Sparring.Rating != rebuiltSparring.Rating)
                {
                    competitor.Sparring = rebuiltSparring;
                    changed = true;
                }

                if (competitor.Specialtekniker.Points != rebuiltSpecialtekniker.Points || competitor.Specialtekniker.Rating != rebuiltSpecialtekniker.Rating)
                {
                    competitor.Specialtekniker = rebuiltSpecialtekniker;
                    changed = true;
                }

                if (competitor.Kross.Points != rebuiltKross.Points || competitor.Kross.Rating != rebuiltKross.Rating)
                {
                    competitor.Kross = rebuiltKross;
                    changed = true;
                }

                if (competitor.CurrentAgeClass != rebuiltAgeClass)
                {
                    competitor.CurrentAgeClass = rebuiltAgeClass;
                    changed = true;
                }

                if (competitor.LatestCompetitionAgeClass != rebuiltLatestCompetitionAgeClass)
                {
                    competitor.LatestCompetitionAgeClass = rebuiltLatestCompetitionAgeClass;
                    changed = true;
                }

                var rebuiltLatestBelt = ResolveLatestBelt(competitor.Results);
                if (competitor.LatestBelt.Level != rebuiltLatestBelt.Level)
                {
                    competitor.LatestBelt = rebuiltLatestBelt;
                    changed = true;
                }
            }

            return changed;
        }

        public static bool RebuildRankingSnapshots(List<Competitor> competitors)
        {
            var changed = false;
            var calculatedAt = DateTime.UtcNow;
            var existingPositions = competitors.ToDictionary(
                competitor => competitor.Id,
                competitor => new
                {
                    TotalPosition = competitor.RankingSnapshot?.TotalPosition ?? 0,
                    TulPosition = competitor.RankingSnapshot?.Tul?.Position ?? 0,
                    SparringPosition = competitor.RankingSnapshot?.Sparring?.Position ?? 0,
                    SpecialteknikerPosition = competitor.RankingSnapshot?.Specialtekniker?.Position ?? 0,
                    KrossPosition = competitor.RankingSnapshot?.Kross?.Position ?? 0
                });

            foreach (var competitor in competitors)
            {
                var snapshot = new CompetitorRankingSnapshot
                {
                    CompetitorId = competitor.Id,
                    CalculatedAt = calculatedAt,
                    TotalPoints = competitor.TotalPoints,
                    TotalRating = competitor.TotalRating,
                    TotalPosition = 0,
                    Tul = new DisciplineScoreSummary
                    {
                        Points = competitor.Tul.Points,
                        Rating = competitor.Tul.Rating,
                        Position = 0
                    },
                    Sparring = new DisciplineScoreSummary
                    {
                        Points = competitor.Sparring.Points,
                        Rating = competitor.Sparring.Rating,
                        Position = 0
                    },
                    Specialtekniker = new DisciplineScoreSummary
                    {
                        Points = competitor.Specialtekniker.Points,
                        Rating = competitor.Specialtekniker.Rating,
                        Position = 0
                    },
                    Kross = new DisciplineScoreSummary
                    {
                        Points = competitor.Kross.Points,
                        Rating = competitor.Kross.Rating,
                        Position = 0
                    }
                };

                if (HasRankingSnapshotChanged(competitor.RankingSnapshot, snapshot))
                {
                    competitor.RankingSnapshot = snapshot;
                    changed = true;
                }
                else
                {
                    competitor.RankingSnapshot.CalculatedAt = calculatedAt;
                }
            }

            foreach (var genderGroup in competitors.Where(c => c.IsMale.HasValue).GroupBy(c => c.IsMale!.Value))
            {
                ApplyPositions(
                    RankingPositionService
                        .OrderByScoreAndMedals(
                            genderGroup,
                            c => c.RankingSnapshot.TotalPoints,
                            c => GetMedalCount(c, CompetitionDisciplineE.Alla, 1),
                            c => GetMedalCount(c, CompetitionDisciplineE.Alla, 2),
                            c => GetMedalCount(c, CompetitionDisciplineE.Alla, 3))
                        .ThenBy(c => c.FullName, SwedishNameComparer.Instance)
                        .ThenBy(c => c.Team, SwedishNameComparer.Instance)
                        .ToList(),
                    (left, right) => HaveSameSnapshotRank(left, right, CompetitionDisciplineE.Alla),
                    (competitor, position) => competitor.RankingSnapshot.TotalPosition = position);

                ApplyPositions(
                    RankingPositionService
                        .OrderByScoreAndMedals(
                            genderGroup,
                            c => c.RankingSnapshot.Tul.Points,
                            c => GetMedalCount(c, CompetitionDisciplineE.Tul, 1),
                            c => GetMedalCount(c, CompetitionDisciplineE.Tul, 2),
                            c => GetMedalCount(c, CompetitionDisciplineE.Tul, 3))
                        .ThenBy(c => c.FullName, SwedishNameComparer.Instance)
                        .ThenBy(c => c.Team, SwedishNameComparer.Instance)
                        .ToList(),
                    (left, right) => HaveSameSnapshotRank(left, right, CompetitionDisciplineE.Tul),
                    (competitor, position) => competitor.RankingSnapshot.Tul.Position = position);

                ApplyPositions(
                    RankingPositionService
                        .OrderByScoreAndMedals(
                            genderGroup,
                            c => c.RankingSnapshot.Sparring.Points,
                            c => GetMedalCount(c, CompetitionDisciplineE.Sparring, 1),
                            c => GetMedalCount(c, CompetitionDisciplineE.Sparring, 2),
                            c => GetMedalCount(c, CompetitionDisciplineE.Sparring, 3))
                        .ThenBy(c => c.FullName, SwedishNameComparer.Instance)
                        .ThenBy(c => c.Team, SwedishNameComparer.Instance)
                        .ToList(),
                    (left, right) => HaveSameSnapshotRank(left, right, CompetitionDisciplineE.Sparring),
                    (competitor, position) => competitor.RankingSnapshot.Sparring.Position = position);

                ApplyPositions(
                    RankingPositionService
                        .OrderByScoreAndMedals(
                            genderGroup,
                            c => c.RankingSnapshot.Specialtekniker.Points,
                            c => GetMedalCount(c, CompetitionDisciplineE.Specialtekniker, 1),
                            c => GetMedalCount(c, CompetitionDisciplineE.Specialtekniker, 2),
                            c => GetMedalCount(c, CompetitionDisciplineE.Specialtekniker, 3))
                        .ThenBy(c => c.FullName, SwedishNameComparer.Instance)
                        .ThenBy(c => c.Team, SwedishNameComparer.Instance)
                        .ToList(),
                    (left, right) => HaveSameSnapshotRank(left, right, CompetitionDisciplineE.Specialtekniker),
                    (competitor, position) => competitor.RankingSnapshot.Specialtekniker.Position = position);

                ApplyPositions(
                    RankingPositionService
                        .OrderByScoreAndMedals(
                            genderGroup,
                            c => c.RankingSnapshot.Kross.Points,
                            c => GetMedalCount(c, CompetitionDisciplineE.Kross, 1),
                            c => GetMedalCount(c, CompetitionDisciplineE.Kross, 2),
                            c => GetMedalCount(c, CompetitionDisciplineE.Kross, 3))
                        .ThenBy(c => c.FullName, SwedishNameComparer.Instance)
                        .ThenBy(c => c.Team, SwedishNameComparer.Instance)
                        .ToList(),
                    (left, right) => HaveSameSnapshotRank(left, right, CompetitionDisciplineE.Kross),
                    (competitor, position) => competitor.RankingSnapshot.Kross.Position = position);
            }

            foreach (var competitor in competitors)
            {
                var previous = existingPositions[competitor.Id];
                if (previous.TotalPosition != competitor.RankingSnapshot.TotalPosition ||
                    previous.TulPosition != competitor.RankingSnapshot.Tul.Position ||
                    previous.SparringPosition != competitor.RankingSnapshot.Sparring.Position ||
                    previous.SpecialteknikerPosition != competitor.RankingSnapshot.Specialtekniker.Position ||
                    previous.KrossPosition != competitor.RankingSnapshot.Kross.Position)
                {
                    changed = true;
                    break;
                }
            }

            return changed;
        }

        static void ApplyPositions(
            List<Competitor> competitors,
            Func<Competitor, Competitor, bool> sameRank,
            Action<Competitor, int> applyPosition)
        {
            RankingPositionService.ApplySharedPositions(competitors, sameRank, applyPosition);
        }

        static bool HaveSameSnapshotRank(
            Competitor left,
            Competitor right,
            CompetitionDisciplineE discipline)
        {
            return discipline switch
            {
                CompetitionDisciplineE.Tul => RankingPositionService.HaveSameScoreAndMedals(
                    left,
                    right,
                    c => c.RankingSnapshot.Tul.Points,
                    c => GetMedalCount(c, CompetitionDisciplineE.Tul, 1),
                    c => GetMedalCount(c, CompetitionDisciplineE.Tul, 2),
                    c => GetMedalCount(c, CompetitionDisciplineE.Tul, 3)),
                CompetitionDisciplineE.Sparring => RankingPositionService.HaveSameScoreAndMedals(
                    left,
                    right,
                    c => c.RankingSnapshot.Sparring.Points,
                    c => GetMedalCount(c, CompetitionDisciplineE.Sparring, 1),
                    c => GetMedalCount(c, CompetitionDisciplineE.Sparring, 2),
                    c => GetMedalCount(c, CompetitionDisciplineE.Sparring, 3)),
                CompetitionDisciplineE.Specialtekniker => RankingPositionService.HaveSameScoreAndMedals(
                    left,
                    right,
                    c => c.RankingSnapshot.Specialtekniker.Points,
                    c => GetMedalCount(c, CompetitionDisciplineE.Specialtekniker, 1),
                    c => GetMedalCount(c, CompetitionDisciplineE.Specialtekniker, 2),
                    c => GetMedalCount(c, CompetitionDisciplineE.Specialtekniker, 3)),
                CompetitionDisciplineE.Kross => RankingPositionService.HaveSameScoreAndMedals(
                    left,
                    right,
                    c => c.RankingSnapshot.Kross.Points,
                    c => GetMedalCount(c, CompetitionDisciplineE.Kross, 1),
                    c => GetMedalCount(c, CompetitionDisciplineE.Kross, 2),
                    c => GetMedalCount(c, CompetitionDisciplineE.Kross, 3)),
                _ => RankingPositionService.HaveSameScoreAndMedals(
                    left,
                    right,
                    c => c.RankingSnapshot.TotalPoints,
                    c => GetMedalCount(c, CompetitionDisciplineE.Alla, 1),
                    c => GetMedalCount(c, CompetitionDisciplineE.Alla, 2),
                    c => GetMedalCount(c, CompetitionDisciplineE.Alla, 3))
            };
        }

        static int GetMedalCount(Competitor competitor, CompetitionDisciplineE discipline, int placement)
        {
            return competitor.Results.Count(result =>
                result.Placement == placement &&
                (discipline == CompetitionDisciplineE.Alla || result.Discipline == discipline));
        }

        static bool HasRankingSnapshotChanged(CompetitorRankingSnapshot? current, CompetitorRankingSnapshot updated)
        {
            if (current == null)
                return true;

            return current.CompetitorId != updated.CompetitorId ||
                   current.TotalPoints != updated.TotalPoints ||
                   current.TotalRating != updated.TotalRating ||
                   current.TotalPosition != updated.TotalPosition ||
                   current.Tul.Points != updated.Tul.Points ||
                   current.Tul.Rating != updated.Tul.Rating ||
                   current.Tul.Position != updated.Tul.Position ||
                   current.Sparring.Points != updated.Sparring.Points ||
                   current.Sparring.Rating != updated.Sparring.Rating ||
                   current.Sparring.Position != updated.Sparring.Position ||
                   current.Specialtekniker.Points != updated.Specialtekniker.Points ||
                   current.Specialtekniker.Rating != updated.Specialtekniker.Rating ||
                   current.Specialtekniker.Position != updated.Specialtekniker.Position ||
                   current.Kross.Points != updated.Kross.Points ||
                   current.Kross.Rating != updated.Kross.Rating ||
                   current.Kross.Position != updated.Kross.Position;
        }

        static string BuildResultFingerprint(CompetitionResult result)
        {
            return string.Join("|",
                result.CompetitionDate.ToString("O"),
                result.CompetitionName.Trim(),
                result.CompetitionCity.Trim(),
                result.Division.Trim(),
                result.Discipline,
                result.Placement,
                result.PointsAwarded,
                result.RatingAwarded);
        }

        static CompetitionFileInfo ParseCompetitionInfo(string fileName, string? text)
        {
            var fallback = ParseCompetitionInfoFromFileName(fileName);
            var parsedFromContent = ParseCompetitionInfoFromContent(text);

            return new CompetitionFileInfo
            {
                Name = !string.IsNullOrWhiteSpace(parsedFromContent.Name) ? parsedFromContent.Name : fallback.Name,
                City = !string.IsNullOrWhiteSpace(parsedFromContent.City) ? parsedFromContent.City : fallback.City,
                Date = parsedFromContent.Date != DateTime.MinValue ? parsedFromContent.Date : fallback.Date
            };
        }

        static CompetitionFileInfo ParseCompetitionInfoFromContent(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new CompetitionFileInfo();

            var lines = text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(NormalizeMetadataLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (lines.Count < 2)
                return new CompetitionFileInfo();

            var name = lines[0];
            var dateLine = lines[1];
            if (!DateTime.TryParseExact(
                dateLine,
                new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "yyyy-MM-dd" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            {
                // Older files may start directly with a category row. In that case,
                // fall back to filename-based competition metadata instead.
                return new CompetitionFileInfo();
            }

            var city = string.Empty;

            if (lines.Count > 2)
            {
                city = ParseCompetitionCityFromAddress(lines[2]);
            }

            return new CompetitionFileInfo
            {
                Name = name,
                City = city,
                Date = date
            };
        }

        static string NormalizeMetadataLine(string value)
        {
            return value
                .Trim()
                .TrimStart('\uFEFF');
        }

        static CompetitionFileInfo ParseCompetitionInfoFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return new CompetitionFileInfo();

            var date = DateTime.MinValue;
            if (fileName.Length >= 10)
            {
                var datePart = fileName[..10];
                if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    date = parsedDate;
                }
            }

            var baseName = Path.GetFileNameWithoutExtension(fileName).Trim();
            if (baseName.Length <= 11)
            {
                return new CompetitionFileInfo
                {
                    Name = ToTitleWords(baseName.Replace('_', ' ')),
                    Date = date
                };
            }

            var remainder = baseName.Substring(11);
            if (remainder.EndsWith("_resultat", StringComparison.OrdinalIgnoreCase))
            {
                remainder = remainder[..^9];
            }
            else if (remainder.EndsWith(" resultat", StringComparison.OrdinalIgnoreCase))
            {
                remainder = remainder[..^9];
            }
            else if (remainder.EndsWith("-resultat", StringComparison.OrdinalIgnoreCase))
            {
                remainder = remainder[..^9];
            }

            remainder = remainder.Trim('_', ' ', '-');

            string city = string.Empty;
            var cityMatch = Regex.Match(remainder, @"\[(?<city>[^\]]+)\]\s*$");
            if (cityMatch.Success)
            {
                city = ParseCompetitionCity(cityMatch.Groups["city"].Value);
                remainder = remainder[..cityMatch.Index].Trim('_', ' ', '-');
            }

            remainder = remainder.Replace('_', ' ').Replace('-', ' ');
            var name = ToTitleWords(remainder);

            return new CompetitionFileInfo
            {
                Name = name,
                City = city,
                Date = date
            };
        }

        static string ToTitleWords(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var parts = value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(TitleCaseWord);

            return string.Join(' ', parts);
        }

        static string TitleCaseWord(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            if (value.Length == 1)
                return value.ToUpperInvariant();

            return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
        }

        static string ParseCompetitionCity(string value)
        {
            var cityCode = value.Trim().ToLowerInvariant();

            return cityCode switch
            {
                "gbg" => "Göteborg",
                "skv" => "Skövde",
                "sthlm" => "Stockholm",
                "ljb" => "Ljungby",
                _ => ToTitleWords(cityCode.Replace('_', ' ').Replace('-', ' '))
            };
        }

        static string ParseCompetitionCityFromAddress(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var trimmed = value.Trim();
            var postalCodeMatch = Regex.Match(trimmed, @"\b\d{3}\s?\d{2}\s+(?<city>.+)$");
            if (postalCodeMatch.Success)
            {
                return ToTitleWords(postalCodeMatch.Groups["city"].Value.Trim());
            }

            var commaSegments = trimmed
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (commaSegments.Length > 1)
            {
                return ToTitleWords(commaSegments[^1]);
            }

            return string.Empty;
        }

    }

    sealed class CompetitionFileInfo
    {
        public string Name { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public DateTime Date { get; init; }
    }


}
