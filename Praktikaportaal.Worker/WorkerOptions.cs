namespace Praktikaportaal.Worker;

/// <summary>Settings of the Worker ("Worker" configuration section).</summary>
public class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Base URL of the Praktikaportaal API.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5117";

    /// <summary>
    /// Credentials of the Service account. Never put real values in appsettings.json: use
    /// user-secrets (<c>dotnet user-secrets set Worker:ServicePassword ...</c>) or environment variables
    /// (<c>Worker__ServicePassword</c>).
    /// </summary>
    public string ServiceEmail { get; set; } = string.Empty;

    public string ServicePassword { get; set; } = string.Empty;

    /// <summary>Local time of day when the digest runs, "HH:mm".</summary>
    public string RunAt { get; set; } = "07:00";

    /// <summary>Testing override: run once immediately when the Worker starts, then follow the schedule.</summary>
    public bool RunOnStartup { get; set; }

    /// <summary>Testing override: if &gt; 0, run every N seconds instead of daily at <see cref="RunAt"/>.</summary>
    public int IntervalSeconds { get; set; }

    /// <summary>JSON file that remembers when the last successful run started.</summary>
    public string StateFilePath { get; set; } = "worker-state.json";

    /// <summary>Students fetched per page.</summary>
    public int PageSize { get; set; } = 50;

    /// <summary>Internships listed in one digest message.</summary>
    public int MaxItemsInDigest { get; set; } = 5;

    /// <summary>Attempts per API call for network errors and 5xx responses.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Pause between attempts (multiplied by the attempt number).</summary>
    public int RetryDelaySeconds { get; set; } = 2;

    public TimeOnly RunAtTime => TimeOnly.ParseExact(RunAt, "HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Returns an error message if the settings are unusable; otherwise null.</summary>
    public string? Validate()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out _))
            return "Worker:ApiBaseUrl must be an absolute URL.";
        if (string.IsNullOrWhiteSpace(ServiceEmail) || string.IsNullOrWhiteSpace(ServicePassword))
            return "Worker:ServiceEmail and Worker:ServicePassword are required (use user-secrets or environment variables).";
        if (!TimeOnly.TryParseExact(RunAt, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
            return "Worker:RunAt must look like 07:00.";
        if (PageSize is < 1 or > 100)
            return "Worker:PageSize must be between 1 and 100 (the API limit).";
        if (MaxItemsInDigest is < 1 or > 50)
            return "Worker:MaxItemsInDigest must be between 1 and 50.";
        if (MaxAttempts < 1)
            return "Worker:MaxAttempts must be at least 1.";
        return null;
    }
}
