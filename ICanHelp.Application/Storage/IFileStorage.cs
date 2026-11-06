namespace ICanHelp.Application.Storage;

/// <summary>
/// Where uploaded files are kept (implementation: <c>Infrastructure/Storage/LocalFileStorage</c>; later e.g. Azure Blob).
/// Paths are relative and chosen by the application (<see cref="ImageFiles.GetStoragePath"/>), so the domain can
/// record the image before the file is written.
/// </summary>
public interface IFileStorage
{
    /// <summary>Writes the file under <paramref name="path"/> (overwrites if it exists).</summary>
    Task SaveAsync(string path, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Opens the file for reading, or returns null if it does not exist. The caller disposes the stream.</summary>
    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Deletes the file. A missing file is not an error.</summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
