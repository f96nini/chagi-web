using System.Globalization;
using System.Text.Json;

namespace ITF_Taekwondo_Gradering.WebConnection;

public sealed class CalendarEntry
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string MonthName { get; init; } = string.Empty;
    public string DayText { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime SortDate { get; init; }
    public DateTime EndDate { get; init; }
    public string Title { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string? Url { get; init; }
    public bool Tentative { get; init; }
}

public static class CalendarAdditionalEvents
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    public static List<CalendarEntry> Parse(string json)
    {
        var file = JsonSerializer.Deserialize<CalendarAdditionalFile>(json, JsonOptions)
            ?? throw new InvalidDataException("Kalenderfilen är tom.");
        if (file.Version != 1 || file.Events is null)
            throw new InvalidDataException("Kalenderfilen har ett ogiltigt format.");

        var entries = new List<CalendarEntry>();
        foreach (var item in file.Events)
        {
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.City) ||
                string.IsNullOrWhiteSpace(item.Country) ||
                !DateTime.TryParseExact(item.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var start))
                throw new InvalidDataException("En kalenderhändelse saknar namn, plats eller giltigt startdatum.");

            var end = start;
            if (!string.IsNullOrWhiteSpace(item.EndDate) &&
                (!DateTime.TryParseExact(item.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out end) || end < start))
                throw new InvalidDataException($"Ogiltigt slutdatum för {item.Title}.");

            string? url = null;
            if (!string.IsNullOrWhiteSpace(item.Url))
            {
                if (!Uri.TryCreate(item.Url, UriKind.Absolute, out var link) ||
                    link.Scheme is not ("https" or "http"))
                    throw new InvalidDataException($"Ogiltig webbadress för {item.Title}.");
                url = link.AbsoluteUri;
            }

            var monthName = Swedish.DateTimeFormat.GetMonthName(start.Month);
            entries.Add(new CalendarEntry
            {
                Year = start.Year,
                Month = start.Month,
                MonthName = char.ToUpperInvariant(monthName[0]) + monthName[1..],
                DayText = FormatDay(start, end),
                Description = $"Tävling: {item.Title} i {item.City}, {item.Country}" +
                    (item.Tentative ? " (preliminärt datum)" : string.Empty),
                SortDate = start,
                EndDate = end,
                Title = item.Title.Trim(),
                City = item.City.Trim(),
                Country = item.Country.Trim(),
                Url = url,
                Tentative = item.Tentative
            });
        }
        return entries;
    }

    public static List<CalendarEntry> Merge(IEnumerable<CalendarEntry> itfEntries,
        IEnumerable<CalendarEntry> additionalEntries)
    {
        var additions = additionalEntries.ToList();
        return itfEntries
            .Where(entry => !additions.Any(addition => entry.SortDate.Date == addition.SortDate.Date &&
                entry.Description.Contains(addition.Title, StringComparison.OrdinalIgnoreCase)))
            .Concat(additions)
            .OrderBy(entry => entry.SortDate)
            .ThenBy(entry => entry.Description, StringComparer.CurrentCulture)
            .ToList();
    }

    static string FormatDay(DateTime start, DateTime end) =>
        start == end ? start.Day.ToString(CultureInfo.InvariantCulture) :
        start.Year == end.Year && start.Month == end.Month ? $"{start.Day}–{end.Day}" :
        start.Year == end.Year ? $"{start.Day}/{start.Month}–{end.Day}/{end.Month}" :
        $"{start:dd/MM/yyyy}–{end:dd/MM/yyyy}";

    sealed class CalendarAdditionalFile
    {
        public int Version { get; init; }
        public List<CalendarAdditionalEvent>? Events { get; init; }
    }

    sealed class CalendarAdditionalEvent
    {
        public string StartDate { get; init; } = string.Empty;
        public string? EndDate { get; init; }
        public string Title { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public string? Url { get; init; }
        public bool Tentative { get; init; }
    }
}
