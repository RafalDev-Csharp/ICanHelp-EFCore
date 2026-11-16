namespace ICanHelp.Application.Storage;

/// <summary>
/// Rules for uploaded images: allowed formats (JPEG, PNG, WebP), max size, file name and storage path.
/// The format is recognised from the first bytes of the file ("magic numbers"), not from the name or the
/// Content-Type sent by the client – both are easy to fake.
/// </summary>
public static class ImageFiles
{
    /// <summary>Max size of one image: 5 MB.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    private const int MaxFileNameLength = 100;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Recognised image format: stored extension + Content-Type.</summary>
    public sealed record Format(string Extension, string ContentType);

    public static readonly Format Jpeg = new(".jpg", "image/jpeg");
    public static readonly Format Png = new(".png", "image/png");
    public static readonly Format Webp = new(".webp", "image/webp");

    /// <summary>JPEG: FF D8 FF; PNG: 89 50 4E 47 0D 0A 1A 0A; WebP: "RIFF" ???? "WEBP". Null = not an allowed image.</summary>
    public static Format? Detect(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return Jpeg;
        }

        if (content.StartsWith(PngSignature))
        {
            return Png;
        }

        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        return null;
    }

    /// <summary>Content-Type for a stored extension (".jpg" → "image/jpeg").</summary>
    public static string GetContentType(string extension)
        => new[] { Jpeg, Png, Webp }.FirstOrDefault(f => f.Extension == extension)?.ContentType
           ?? "application/octet-stream";

    /// <summary>Relative path in the storage, e.g. "{announcementId}/{imageId}.jpg" (ids without dashes).</summary>
    public static string GetStoragePath(Guid announcementId, Guid imageId, Format format)
        => $"{announcementId:N}/{imageId:N}{format.Extension}";

    /// <summary>
    /// File name only for display: without the client's directory, trimmed to 100 chars (keeping the extension).
    /// Empty → "image.jpg".
    /// </summary>
    public static string NormalizeFileName(string? fileName, Format format)
    {
        // Browsers on Windows sometimes send the full path ("C:\fakepath\kran.jpg").
        var name = (fileName ?? string.Empty).Replace('\\', '/').Split('/')[^1].Trim();
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());

        if (string.IsNullOrWhiteSpace(name))
        {
            return "image" + format.Extension;
        }

        if (name.Length <= MaxFileNameLength)
        {
            return name;
        }

        var extension = Path.GetExtension(name);
        if (extension.Length is 0 or > 10)
        {
            return name[..MaxFileNameLength];
        }

        return name[..(MaxFileNameLength - extension.Length)] + extension;
    }
}
