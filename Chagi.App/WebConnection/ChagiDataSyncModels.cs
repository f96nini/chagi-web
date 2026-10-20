namespace ITF_Taekwondo_Gradering.WebConnection;

public enum ChagiDataSyncOutcome
{
    NoChange = 0,
    Updated = 1,
    Failed = 2,
    Skipped = 3
}

public enum ChagiDataSyncTrigger
{
    Automatic = 0,
    Manual = 1
}

public sealed class ChagiDataSyncResult
{
    public int FilesDownloaded { get; init; }
    public long BytesDownloaded { get; init; }
    public bool ManifestNotModified { get; init; }
    public ChagiDataSyncOutcome Outcome { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime AttemptedAtUtc { get; init; }
    public DateTime? LastSuccessfulSyncUtc { get; init; }
}

public sealed class ChagiDataSyncStatus
{
    public DateTime? LastAttemptUtc { get; init; }
    public DateTime? LastSuccessfulSyncUtc { get; init; }
    public ChagiDataSyncOutcome? LastOutcome { get; init; }
    public string LastMessage { get; init; } = string.Empty;
}
