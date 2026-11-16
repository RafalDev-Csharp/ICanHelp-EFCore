using ICanHelp.Application.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ICanHelp.Infrastructure.Storage;

/// <summary>
/// Files on the local disk, in the folder from <see cref="StorageOptions.ImagesPath"/>. Good for development and one server;
/// with several instances replace it with shared storage (e.g. Azure Blob) – only this class changes.
/// </summary>
internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment)
    {
        var configured = options.Value.ImagesPath;
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured)));
    }

    public async Task SaveAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        Stream? stream = File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true)
            : null;

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        // Empty folder of the announcement is not needed any more.
        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null && directory != _root && Directory.Exists(directory)
            && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }

        return Task.CompletedTask;
    }

    // Paths come from the application, but just in case: never leave the root folder ("../../etc").
    private string GetFullPath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, path));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path '{path}' is outside of the storage folder.");
        }

        return fullPath;
    }
}
