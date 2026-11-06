using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Storage;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

/// <summary>
/// Kolejność: (1) wczytanie i sprawdzenie pliku (rozmiar, format) – bez dotykania bazy, (2) w transakcji: reguły domeny
/// (<c>Announcement.AddImage</c>: właściciel, Open, limit), dopiero potem zapis pliku, (3) commit.
/// Gdy cokolwiek po zapisie pliku się nie uda (np. konflikt współbieżności), plik jest kasowany – nie zostają sieroty.
/// </summary>
internal sealed class AddAnnouncementImageHandler : ICommandHandler<AddAnnouncementImage>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _storage;
    private readonly IClock _clock;

    public AddAnnouncementImageHandler(IAnnouncementRepository announcements, IUserRepository users,
        IUnitOfWork unitOfWork, IFileStorage storage, IClock clock)
    {
        _announcements = announcements;
        _users = users;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _clock = clock;
    }

    public async Task HandleAsync(AddAnnouncementImage command, CancellationToken cancellationToken = default)
    {
        var content = await ReadLimitedAsync(command.Content, ImageFiles.MaxBytes, cancellationToken);
        var format = ImageFiles.Detect(content)
                     ?? throw new InvalidImageException("Only JPEG, PNG and WebP images are allowed.");

        var path = ImageFiles.GetStoragePath(command.AnnouncementId, command.ImageId, format);
        var fileSaved = false;

        try
        {
            await _unitOfWork.ExecuteAsync(async () =>
            {
                await _users.GetActiveOrThrowAsync(command.ActorId, cancellationToken);
                var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);

                announcement.AddImage(
                    new UserId(command.ActorId),
                    new Id(command.ImageId),
                    ImageFiles.NormalizeFileName(command.FileName, format),
                    format.Extension,
                    path,
                    _clock.UtcNow);

                using var stream = new MemoryStream(content, writable: false);
                await _storage.SaveAsync(path, stream, cancellationToken);
                fileSaved = true;
            }, cancellationToken);
        }
        catch when (fileSaved)
        {
            await _storage.DeleteAsync(path, CancellationToken.None);
            throw;
        }
    }

    // Nie ufamy nagłówkom (Content-Length, rozmiar z formularza) – liczymy bajty sami i przerywamy po przekroczeniu.
    private static async Task<byte[]> ReadLimitedAsync(Stream source, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                throw new InvalidImageException($"An image cannot be larger than {maxBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0)
        {
            throw new InvalidImageException("The file is empty.");
        }

        return buffer.ToArray();
    }
}

/// <summary>Usuwa zdjęcie z ogłoszenia, a po udanym zapisie – plik z magazynu.</summary>
internal sealed class RemoveAnnouncementImageHandler : ICommandHandler<RemoveAnnouncementImage>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorage _storage;
    private readonly IClock _clock;

    public RemoveAnnouncementImageHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork,
        IFileStorage storage, IClock clock)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _clock = clock;
    }

    public async Task HandleAsync(RemoveAnnouncementImage command, CancellationToken cancellationToken = default)
    {
        string? path = null;

        await _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            var image = announcement.RemoveImage(new UserId(command.ActorId), new Id(command.ImageId), _clock.UtcNow);
            path = image.FilePath;
        }, cancellationToken);

        // Dopiero po commicie: gdyby zapis się nie udał, zdjęcie w bazie nadal miałoby swój plik.
        if (path is not null)
        {
            await _storage.DeleteAsync(path, cancellationToken);
        }
    }
}
