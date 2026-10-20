using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ITF_Taekwondo_Gradering.WebConnection;

public static class CalendarSyncService
{
    const string CalendarUrl = "https://itfsverige.se/kalendarium/kalender.htm";

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

            try
            {
                var html = await DownloadCalendarHtmlAsync().ConfigureAwait(false);
                var entries = ParseCalendar(html);
                if (entries.Count == 0)
                    throw new InvalidOperationException("Kalendern innehöll inga parsade rader.");

                var cache = new CalendarCache
                {
                    SourceUrl = CalendarUrl,
                    DownloadedUtc = DateTime.UtcNow,
                    Fingerprint = BuildFingerprint(html),
                    Entries = entries
                };
                await SaveCacheAsync(cache).ConfigureAwait(false);

                return new CalendarSyncResult
                {
                    Entries = entries,
                    DownloadedUtc = cache.DownloadedUtc,
                    LoadedFromCache = false
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                var cache = await LoadCacheAsync().ConfigureAwait(false);
                if (cache != null && cache.Entries.Count > 0)
                {
                    return new CalendarSyncResult
                    {
                        Entries = cache.Entries,
                        DownloadedUtc = cache.DownloadedUtc,
                        LoadedFromCache = true
                    };
                }

                return new CalendarSyncResult
                {
                    Entries = new List<CalendarEntry>(),
                    DownloadedUtc = attemptedAtUtc,
                    LoadedFromCache = false,
                    ErrorMessage = "Kunde inte läsa kalendern."
                };
            }
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
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class CalendarEntry
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string MonthName { get; init; } = string.Empty;
    public string DayText { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime SortDate { get; init; }
    public DateTime EndDate { get; init; }
}

sealed class CalendarCache
{
    public string SourceUrl { get; init; } = string.Empty;
    public DateTime DownloadedUtc { get; init; }
    public string Fingerprint { get; init; } = string.Empty;
    public List<CalendarEntry> Entries { get; init; } = new();
}
