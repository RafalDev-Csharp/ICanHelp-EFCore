using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Queries;
using ICanHelp.Application.Storage;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ICanHelp.Infrastructure.DAL.Handlers;

internal sealed class GetAnnouncementImageHandler : IQueryHandler<GetAnnouncementImage, ImageFileDto?>
{
    private readonly ICanHelpDbContext _db;
    private readonly IFileStorage _storage;

    public GetAnnouncementImageHandler(ICanHelpDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<ImageFileDto?> HandleAsync(GetAnnouncementImage query, CancellationToken cancellationToken = default)
    {
        var announcementId = new Id(query.AnnouncementId);
        var imageId = new Id(query.ImageId);

        var image = await _db.Images
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == imageId && i.AnnouncementId == announcementId, cancellationToken);

        if (image is null)
        {
            return null;
        }

        var content = await _storage.OpenReadAsync(image.FilePath, cancellationToken);

        return content is null
            ? null
            : new ImageFileDto(content, ImageFiles.GetContentType(image.FileExtension), image.FileName);
    }
}
