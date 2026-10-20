using System.Globalization;
using ITF_Taekwondo_Gradering.Logic;
using ITF_Taekwondo_Gradering.Models.Ranking;
using ITF_Taekwondo_Gradering.WebConnection;

namespace ITF_Taekwondo_Gradering;

public partial class InformationPage : ContentPage
{
    bool _hasLoadedDataInformation;

    public InformationPage()
    {
        InitializeComponent();
        DataInformationLabel.Text = BuildDataInformationText("DATUM");
        LastSyncAttemptLabel.Text = "Senaste synkförsök: okänt";
        LastSyncSuccessLabel.Text = "Senaste lyckade synkning: okänt";
        LastSyncStatusLabel.Text = "Status: ingen synkning utförd än";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ResultsDataRefreshService.AppearingAsync(this);
    }

    protected override void OnDisappearing()
    {
        ResultsDataRefreshService.Disappearing(this);
        base.OnDisappearing();
    }

    public async Task RefreshDataAsync()
    {
        await RefreshSyncStatusAsync();

        if (!_hasLoadedDataInformation)
        {
            _hasLoadedDataInformation = true;
            await RefreshDataInformationAsync();
        }
    }

    void OnVersionHistoryClicked(object? sender, EventArgs e)
    {
        VersionHistoryContent.IsVisible = !VersionHistoryContent.IsVisible;
        VersionHistoryButton.Text = VersionHistoryContent.IsVisible ? "Dölj versionshistorik" : "Visa versionshistorik";
    }

    async void OnSyncNowClicked(object? sender, EventArgs e)
    {
        SyncNowButton.IsEnabled = false;

        try
        {
            var result = await RunManualSyncAsync();
            if (result.Outcome == ChagiDataSyncOutcome.Updated)
            {
                await RefreshDataInformationAsync();
            }
        }
        finally
        {
            await RefreshSyncStatusAsync();
            SyncNowButton.IsEnabled = true;
        }
    }

    async Task<ChagiDataSyncResult> RunManualSyncAsync()
    {
        ChagiDataSyncResult result = new()
        {
            Outcome = ChagiDataSyncOutcome.Failed,
            Message = "Synkning misslyckades."
        };

        await PageWorkService.RunAsync(this, async () =>
        {
            result = await ChagiDataSyncService.SyncAsync(ChagiDataSyncTrigger.Manual);
        }, "Synkar resultat...");

        return result;
    }

    async Task RefreshDataInformationAsync()
    {
        DataInformationLabel.Text = BuildDataInformationText("Laddar datum...");

        try
        {
            var startDateText = await PageWorkService.RunBackgroundAsync(this, GetStartDateTextAsync, "Laddar datainformation...");
            DataInformationLabel.Text = BuildDataInformationText(startDateText);
        }
        catch
        {
            DataInformationLabel.Text = BuildDataInformationText("DATUM");
        }
    }

    async Task RefreshSyncStatusAsync()
    {
        try
        {
            var status = await ChagiDataSyncService.GetStatusAsync();
            LastSyncAttemptLabel.Text = $"Senaste synkförsök: {FormatDateTime(status.LastAttemptUtc)}";
            LastSyncSuccessLabel.Text = $"Senaste lyckade synkning: {FormatDateTime(status.LastSuccessfulSyncUtc)}";
            LastSyncStatusLabel.Text = string.IsNullOrWhiteSpace(status.LastMessage)
                ? "Status: ingen synkning utförd än"
                : $"Status: {status.LastMessage}";
        }
        catch
        {
            LastSyncAttemptLabel.Text = "Senaste synkförsök: okänt";
            LastSyncSuccessLabel.Text = "Senaste lyckade synkning: okänt";
            LastSyncStatusLabel.Text = "Status: kunde inte läsa synkstatus";
        }
    }

    static async Task<string> GetStartDateTextAsync()
    {
        var competitors = await CompetitorRepository.GetAllAsync();
        var startDate = competitors
            .SelectMany(static competitor => competitor.Results)
            .Where(static result => result.Placement is >= 1 and <= 3)
            .Select(static result => (DateTime?)result.CompetitionDate)
            .Min();

        if (!startDate.HasValue)
            return "DATUM";

        return startDate.Value.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("sv-SE"));
    }

    static string FormatDateTime(DateTime? value)
    {
        if (!value.HasValue)
            return "okänt";

        return value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.GetCultureInfo("sv-SE"));
    }

    static string BuildDataInformationText(string startDateText)
    {
        return $"Appen baseras på resultathistorik från {startDateText} fram till idag.";
    }
}
