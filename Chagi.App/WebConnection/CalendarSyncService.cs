using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ITF_Taekwondo_Gradering.WebConnection;

public static class CalendarSyncService
{
    const string CalendarUrl = "https://itfsverige.se/kalendarium/kalender.htm";
    static readonly Uri AdditionalCalendarUrl = new(new Uri(ChagiVersion.BaseUrl), "calendar/events.json");

    static readonly SemaphoreSlim SyncLock = new(1, 1);
    static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    static readonly Dictionary<string, int> MonthNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JANUARI"] = 1,
        ["FEBRUARI"] = 2,
        ["MARS"] = 3,
        ["APRIL"] = 4,
        ["MAJ"] = 5,
        ["JUNI"] = 6,
        ["JULI"] = 7,
        ["AUGUSTI"] = 8,
        ["SEPTEMBER"] = 9,
        ["OKTOBER"] = 10,
        ["NOVEMBER"] = 11,
        ["DECEMBER"] = 12
    };

    static string CacheDirectory => Path.Combine(FileSystem.AppDataDirectory, "Data", "calendar");
    static string CachePath => Path.Combine(CacheDirectory, "calendar.json");

    public static async Task WarmupAsync()
    {
        await GetCalendarAsync().ConfigureAwait(false);
    }

    public static Task<CalendarSyncResult> GetCalendarAsync()
        => Task.Run(() => GetCalendarCoreAsync());

    static async Task<CalendarSyncResult> GetCalendarCoreAsync()
    {
        await SyncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var attemptedAtUtc = DateTime.UtcNow;
            var previous = await LoadCacheAsync().ConfigureAwait(false);
            var itfEntries = previous is { Version: >= 2 }
                ? previous.ItfEntries : previous?.Entries ?? new List<CalendarEntry>();
            var additionalEntries = previous?.AdditionalEntries ?? new List<CalendarEntry>();
            var fingerprint = previous?.Fingerprint ?? string.Empty;
            var itfFresh = false;
            var additionsFresh = false;

            // Both sources start together so one slow site does not delay the other twice.
            var itfDownload = DownloadCalendarHtmlAsync();
            var additionsDownload = HttpClient.GetStringAsync(AdditionalCalendarUrl);
            try
            {
                var html = await itfDownload.ConfigureAwait(false);
                var parsed = ParseCalendar(html);
                if (parsed.Count == 0)
                    throw new InvalidOperationException("Kalendern innehöll inga parsade rader.");
                itfEntries = parsed;
                fingerprint = BuildFingerprint(html);
                itfFresh = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            try
            {
                additionalEntries = CalendarAdditionalEvents.Parse(
                    await additionsDownload.ConfigureAwait(false));
                additionsFresh = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }

            var entries = CalendarAdditionalEvents.Merge(itfEntries, additionalEntries);
            if (itfFresh || additionsFresh)
                await SaveCacheAsync(new CalendarCache
                {
                    Version = 2,
                    SourceUrl = CalendarUrl,
                    AdditionalSourceUrl = AdditionalCalendarUrl.AbsoluteUri,
                    DownloadedUtc = attemptedAtUtc,
                    Fingerprint = fingerprint,
                    ItfEntries = itfEntries,
                    AdditionalEntries = additionalEntries,
                    Entries = entries
                }).ConfigureAwait(false);

            return new CalendarSyncResult
            {
                Entries = entries,
                DownloadedUtc = itfFresh || additionsFresh ? attemptedAtUtc : previous?.DownloadedUtc ?? attemptedAtUtc,
                LoadedFromCache = !itfFresh && !additionsFresh && entries.Count > 0,
                HasSourceError = !itfFresh || !additionsFresh,
                ErrorMessage = entries.Count == 0 ? "Kunde inte läsa kalendern." : string.Empty
            };
        }
        finally
        {
            SyncLock.Release();
        }
    }

    static async Task<string> DownloadCalendarHtmlAsync()
    {
        var bytes = await HttpClient.GetByteArrayAsync(CalendarUrl).ConfigureAwait(false);
        return Encoding.Latin1.GetString(bytes);
    }

    public static List<CalendarEntry> ParseCalendar(string html)
    {
        var result = new List<CalendarEntry>();
        if (string.IsNullOrWhiteSpace(html))
            return result;

        var currentYear = 0;
        var currentMonthName = string.Empty;
        var currentMonthNumber = 0;
        var rows = Regex.Matches(html, @"<tr\b[^>]*>(?<content>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match row in rows)
        {
            var rowContent = row.Groups["content"].Value;
            var rowText = CleanCellText(rowContent).ToUpperInvariant();

            if (Regex.IsMatch(rowText, @"^20\d{2}$") && int.TryParse(rowText, out var parsedYear))
            {
                currentYear = parsedYear;
                currentMonthName = string.Empty;
                currentMonthNumber = 0;
                continue;
            }

            if (MonthNumbers.TryGetValue(rowText, out var parsedMonthNumber))
            {
                currentMonthName = ToDisplayMonthName(rowText);
                currentMonthNumber = parsedMonthNumber;
                continue;
            }

            if (currentYear == 0 || currentMonthNumber == 0)
                continue;

            var cells = Regex.Matches(rowContent, @"<td\b[^>]*>(?<content>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (cells.Count < 2)
                continue;

            var dayText = CleanCellText(cells[0].Groups["content"].Value);
            var description = CleanDescription(CleanCellText(cells[1].Groups["content"].Value));
            if (string.IsNullOrWhiteSpace(dayText) || string.IsNullOrWhiteSpace(description))
                continue;

            result.Add(new CalendarEntry
            {
                Year = currentYear,
                Month = currentMonthNumber,
                MonthName = currentMonthName,
                DayText = dayText,
                Description = description,
                SortDate = BuildSortDate(currentYear, currentMonthNumber, dayText),
                EndDate = BuildEndDate(currentYear, currentMonthNumber, dayText)
            });
        }

        return result;
    }

    static string CleanCellText(string value)
    {
        var withoutTags = Regex.Replace(value, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    static string CleanDescription(string value)
    {
        return value.Trim().TrimStart('-', ' ', '\u00a0').Trim();
    }

    static string ToDisplayMonthName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var lower = value.ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower[1..];
    }

    static DateTime BuildSortDate(int year, int month, string dayText)
    {
        return BuildDateFromText(year, month, dayText, useLastDate: false);
    }

    static DateTime BuildEndDate(int year, int month, string dayText)
    {
        return BuildDateFromText(year, month, dayText, useLastDate: true);
    }

    static DateTime BuildDateFromText(int year, int month, string dayText, bool useLastDate)
    {
        var normalized = dayText.Replace('–', '-').Trim();
        var dateMatches = Regex.Matches(normalized, @"(?<day>\d{1,2})(?:/(?<month>\d{1,2}))?");
        if (dateMatches.Count == 0)
            return new DateTime(year, month, 1);

        var match = useLastDate ? dateMatches[^1] : dateMatches[0];
        var parsedMonth = month;
        if (match.Groups["month"].Success &&
            int.TryParse(match.Groups["month"].Value, out var monthFromText) &&
            monthFromText is >= 1 and <= 12)
        {
            parsedMonth = monthFromText;
        }

        if (!int.TryParse(match.Groups["day"].Value, out var day))
            day = 1;

        day = Math.Clamp(day, 1, DateTime.DaysInMonth(year, parsedMonth));

        var date = new DateTime(year, parsedMonth, day);
        if (useLastDate)
        {
            var startDate = BuildDateFromText(year, month, dayText, useLastDate: false);
            if (date < startDate)
                date = date.AddYears(1);
        }

        return date;
    }

    static string BuildFingerprint(string html)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(html));
        return Convert.ToHexString(hash);
    }

    static async Task SaveCacheAsync(CalendarCache cache)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            var json = JsonSerializer.Serialize(cache, JsonOptions);
            await File.WriteAllTextAsync(CachePath, json).ConfigureAwait(false);
        }
        catch
        {
            // Cache writes should not block the app from showing freshly downloaded data.
        }
    }

    static async Task<CalendarCache?> LoadCacheAsync()
    {
        try
        {
            if (!File.Exists(CachePath))
                return null;

            var json = await File.ReadAllTextAsync(CachePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize<CalendarCache>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class CalendarSyncResult
{
    public List<CalendarEntry> Entries { get; init; } = new();
    public DateTime DownloadedUtc { get; init; }
    public bool LoadedFromCache { get; init; }
    public bool HasSourceError { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}

sealed class CalendarCache
{
    public int Version { get; init; }
    public string SourceUrl { get; init; } = string.Empty;
    public string AdditionalSourceUrl { get; init; } = string.Empty;
    public DateTime DownloadedUtc { get; init; }
    public string Fingerprint { get; init; } = string.Empty;
    public List<CalendarEntry> ItfEntries { get; init; } = new();
    public List<CalendarEntry> AdditionalEntries { get; init; } = new();
    public List<CalendarEntry> Entries { get; init; } = new();
}
