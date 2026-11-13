using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Praktikaportaal.Core.Interfaces;
using Praktikaportaal.Infrastructure.Options;

namespace Praktikaportaal.Infrastructure.Services;

/// <summary>
/// Stores files in a directory on disk. File names are random GUIDs, so user input never
/// reaches the file system path; keys are also checked against path traversal.
/// </summary>
public partial class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default)
    {
        if (!ExtensionPattern().IsMatch(extension))
            throw new ArgumentException("Invalid file extension.", nameof(extension));

        string key = $"{Guid.NewGuid():N}{extension}";

        await using var file = new FileStream(
            Resolve(key), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);

        return key;
    }

    public Stream OpenRead(string storageKey) =>
        new FileStream(Resolve(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        File.Delete(Resolve(storageKey)); // no exception if the file does not exist
        return Task.CompletedTask;
    }

    /// <summary>Maps a key to a path inside the storage root; rejects anything that would escape it.</summary>
    private string Resolve(string key)
    {
        if (!KeyPattern().IsMatch(key))
            throw new ArgumentException("Invalid storage key.", nameof(key));

        string path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Invalid storage key.", nameof(key));

        return path;
    }

    [GeneratedRegex(@"^\.[a-z0-9]{1,5}$")]
    private static partial Regex ExtensionPattern();

    [GeneratedRegex(@"^[a-f0-9]{32}\.[a-z0-9]{1,5}$")]
    private static partial Regex KeyPattern();
}
