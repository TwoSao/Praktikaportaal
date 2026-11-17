using System.Collections.Concurrent;

namespace Praktikaportaal.API.WebSockets;

/// <summary>
/// Registry of open WebSocket connections by group name (e.g. "company-{companyProfileId}").
/// Thread-safe singleton shared by the socket endpoint and the notifier.
/// </summary>
public class ConnectionGroups
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, SocketConnection>> _groups = new();

    public static string CompanyGroup(Guid companyProfileId) => $"company-{companyProfileId}";

    public void Add(string group, SocketConnection connection) =>
        _groups.GetOrAdd(group, _ => new()).TryAdd(connection.Id, connection);

    public void Remove(string group, SocketConnection connection)
    {
        if (!_groups.TryGetValue(group, out var members))
            return;

        members.TryRemove(connection.Id, out _);
        if (members.IsEmpty)
            _groups.TryRemove(group, out _);
    }

    /// <summary>Snapshot of the group's open connections (safe to iterate while others connect/disconnect).</summary>
    public IReadOnlyList<SocketConnection> GetOpen(string group) =>
        _groups.TryGetValue(group, out var members)
            ? members.Values.Where(c => c.IsOpen).ToList()
            : [];

    public int Count(string group) => _groups.TryGetValue(group, out var members) ? members.Count : 0;
}
