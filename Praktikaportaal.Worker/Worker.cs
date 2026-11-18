using Microsoft.Extensions.Options;

namespace Praktikaportaal.Worker;

/// <summary>
/// Runs <see cref="DigestJob"/> every day at the configured time (07:00 by default) using a
/// <see cref="PeriodicTimer"/>. For testing, <c>Worker:RunOnStartup</c> runs once immediately and
/// <c>Worker:IntervalSeconds</c> replaces the daily schedule with a short fixed interval.
/// </summary>
public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly WorkerOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceScopeFactory scopes, IOptions<WorkerOptions> options, TimeProvider time, ILogger<Worker> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.IntervalSeconds > 0)
            _logger.LogInformation("Worker started: running every {Seconds} s (testing override)", _options.IntervalSeconds);
        else
            _logger.LogInformation("Worker started: running daily at {RunAt} local time", _options.RunAt);

        if (_options.RunOnStartup)
            await RunSafelyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextDelay();
            _logger.LogInformation("Next run in {Delay:hh\\:mm\\:ss} (at {At:yyyy-MM-dd HH:mm} local time)",
                delay, _time.GetLocalNow().DateTime + delay);

            // A fresh timer per cycle keeps the run aligned with the clock (the length of a day changes with DST).
            using var timer = new PeriodicTimer(delay, _time);
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunSafelyAsync(stoppingToken);
        }

        _logger.LogInformation("Worker stopped");
    }

    private TimeSpan NextDelay() =>
        _options.IntervalSeconds > 0
            ? TimeSpan.FromSeconds(_options.IntervalSeconds)
            : Schedule.DelayUntilNextRun(_time.GetLocalNow().DateTime, _options.RunAtTime);

    /// <summary>A failed run is logged and retried at the next scheduled time; it never crashes the host.</summary>
    private async Task RunSafelyAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DigestJob>().RunAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Digest run failed; it will be retried at the next scheduled time");
        }
    }
}
