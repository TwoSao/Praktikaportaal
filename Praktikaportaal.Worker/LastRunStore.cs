using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Praktikaportaal.Worker;

/// <summary>Remembers when the last successful run started, so the next run only looks at newer internships.</summary>
public interface ILastRunStore
{
    /// <summary>Time of the last successful run (UTC), or null if the Worker has never run.</summary>
    Task<DateTime?> LoadAsync(CancellationToken ct);

    Task SaveAsync(DateTime lastRunUtc, CancellationToken ct);
}

/// <summary>Stores the time in a small JSON file: <c>{"lastRunUtc":"2026-11-10T07:00:00Z"}</c>.</summary>
public class JsonFileLastRunStore : ILastRunStore
{
    private record State(DateTime LastRunUtc);

    private readonly string _path;
    private readonly ILogger<JsonFileLastRunStore> _logger;

    public JsonFileLastRunStore(IOptions<WorkerOptions> options, ILogger<JsonFileLastRunStore> logger)
    {
        _path = Path.GetFullPath(options.Value.StateFilePath);
        _logger = logger;
    }

    public async Task<DateTime?> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            await using var stream = File.OpenRead(_path);
            var state = await JsonSerializer.DeserializeAsync<State>(stream, cancellationToken: ct);
            return state == null ? null : DateTime.SpecifyKind(state.LastRunUtc, DateTimeKind.Utc);
        }
        catch (JsonException ex)
        {
            // A corrupt state file must not stop the Worker; fall back to the default look-back window.
            _logger.LogWarning(ex, "State file {Path} is not valid JSON; ignoring it", _path);
            return null;
        }
    }

    public async Task SaveAsync(DateTime lastRunUtc, CancellationToken ct)
    {
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Write to a temp file first so a crash cannot leave a half-written state file.
        string temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(new State(DateTime.SpecifyKind(lastRunUtc, DateTimeKind.Utc))), ct);
        File.Move(temp, _path, overwrite: true);
    }
}
