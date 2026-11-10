using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace Praktikaportaal.Worker;

/// <summary>Outcome of one run, for logging and tests.</summary>
public record DigestResult(int NewInternships, int StudentsChecked, int DigestsSent, int Failures);

/// <summary>
/// One daily run: find internships created since the last run, and for every student ask the API which of
/// them match their skills; students with matches get a digest notification.
/// The Worker only triggers things - matching (skill overlap) is calculated by the API.
/// </summary>
public class DigestJob
{
    /// <summary>When the Worker has never run, look back this far.</summary>
    public static readonly TimeSpan DefaultLookBack = TimeSpan.FromDays(1);

    private readonly IServiceApiClient _api;
    private readonly ILastRunStore _store;
    private readonly WorkerOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<DigestJob> _logger;

    public DigestJob(
        IServiceApiClient api,
        ILastRunStore store,
        IOptions<WorkerOptions> options,
        TimeProvider time,
        ILogger<DigestJob> logger)
    {
        _api = api;
        _store = store;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    /// <summary>
    /// Runs the job. Throws if the run as a whole failed (login, listing internships or students) - the last-run
    /// time is then NOT advanced, so the next run covers the same period. A failure for a single student is logged,
    /// counted and skipped; the run still counts as done.
    /// </summary>
    public async Task<DigestResult> RunAsync(CancellationToken ct)
    {
        var runStartedAt = _time.GetUtcNow().UtcDateTime;
        var since = await _store.LoadAsync(ct) ?? runStartedAt - DefaultLookBack;
        _logger.LogInformation("Digest run started; looking for internships created after {Since:u}", since);

        int newInternships = await _api.CountNewInternshipsAsync(since, ct);
        if (newInternships == 0)
        {
            _logger.LogInformation("No new internships since {Since:u}; nothing to send", since);
            await _store.SaveAsync(runStartedAt, ct);
            return new DigestResult(0, 0, 0, 0);
        }

        _logger.LogInformation("{Count} new internship(s) found; matching them against students", newInternships);

        int checkedStudents = 0, sent = 0, failures = 0;

        for (int page = 1; ; page++)
        {
            var students = await _api.GetStudentsAsync(page, _options.PageSize, ct);

            foreach (var student in students.Items)
            {
                ct.ThrowIfCancellationRequested();
                checkedStudents++;

                try
                {
                    if (await SendDigestAsync(student, since, ct))
                        sent++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failures++;
                    _logger.LogError(ex, "Could not process student {StudentId} ({Email}); continuing with the others",
                        student.Id, student.Email);
                }
            }

            if (students.Items.Count == 0 || page * _options.PageSize >= students.TotalCount)
                break;
        }

        await _store.SaveAsync(runStartedAt, ct);
        _logger.LogInformation(
            "Digest run finished: {Students} student(s) checked, {Sent} digest(s) sent, {Failures} failure(s)",
            checkedStudents, sent, failures);

        return new DigestResult(newInternships, checkedStudents, sent, failures);
    }

    private async Task<bool> SendDigestAsync(StudentSummary student, DateTime since, CancellationToken ct)
    {
        var matches = await _api.GetRecommendationsAsync(student.Id, since, _options.MaxItemsInDigest, ct);
        if (matches.TotalCount == 0)
            return false;

        await _api.CreateNotificationAsync(student.UserId, BuildTitle(matches.TotalCount), BuildMessage(matches), ct);
        _logger.LogInformation("Digest sent to {Email}: {Count} matching internship(s)", student.Email, matches.TotalCount);
        return true;
    }

    public static string BuildTitle(int count) =>
        count == 1 ? "1 new internship matches your skills" : $"{count} new internships match your skills";

    public static string BuildMessage(PageOf<Recommendation> matches)
    {
        var text = new StringBuilder();
        foreach (var match in matches.Items)
        {
            if (text.Length > 0)
                text.Append("; ");
            text.Append(CultureInfo.InvariantCulture, $"{match.Internship.Title} at {match.Internship.CompanyName} ({match.OverlapPercent:0.#} %)");
        }

        int more = matches.TotalCount - matches.Items.Count;
        if (more > 0)
            text.Append(CultureInfo.InvariantCulture, $"; and {more} more");

        // The API accepts at most 1000 characters.
        return text.Length <= 1000 ? text.ToString() : text.ToString(0, 997) + "...";
    }
}
