using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

// Photos of the announcement. Adding or removing one is a change of the content like any edit.
public partial class Announcement
{
    public const int MaxImages = 5;

    /// <summary>
    /// The owner adds a photo while the announcement is open. It is a change of the content like any edit:
    /// new revision with the <see cref="AnnouncementFields.Images"/> flag, so pending offers become outdated
    /// (the helper priced the job without seeing this photo).
    /// </summary>
    public Image AddImage(UserId actorId, Id imageId, StringObject fileName, StringObject fileExtension,
        StringObject filePath, DateTime addedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "have images added");
        EnsureOwner(actorId, "add images to");

        if (_images.Count >= MaxImages)
        {
            throw new DomainRuleException($"An announcement cannot have more than {MaxImages} images.");
        }

        if (_images.Any(i => i.Id == imageId))
        {
            throw new DomainRuleException($"Image with id: {imageId} already belongs to this announcement.");
        }

        var before = FormatImages();
        var image = new Image(imageId, Id, fileName, fileExtension, filePath, addedAtUtc);
        _images.Add(image);

        RecordChange([new AnnouncementFieldChange(AnnouncementFields.Images, before, FormatImages())], addedAtUtc);
        return image;
    }

    /// <summary>
    /// The owner removes a photo while the announcement is open (new revision, like <see cref="AddImage"/>).
    /// Returns the removed image, so the caller can delete its file.
    /// </summary>
    public Image RemoveImage(UserId actorId, Id imageId, DateTime changedAtUtc)
    {
        EnsureStatus(AnnouncementStatus.Open, "have images removed");
        EnsureOwner(actorId, "remove images from");

        var image = _images.FirstOrDefault(i => i.Id == imageId) ?? throw new ImageNotFoundException(imageId);

        var before = FormatImages();
        _images.Remove(image);

        RecordChange([new AnnouncementFieldChange(AnnouncementFields.Images, before, FormatImages())], changedAtUtc);
        return image;
    }

    /// <summary>Images in the order they were added (the same moment – by id, so the order is stable).</summary>
    public IReadOnlyList<Image> GetImagesInOrder()
        => _images.OrderBy(i => i.AddedAt).ThenBy(i => i.Id.Value).ToList();

    /// <summary>The thumbnail of the announcement: the oldest image (null if there are no images).</summary>
    public Image? GetThumbnail() => GetImagesInOrder().FirstOrDefault();

    // Images as "before/after" text: ids in the order they were added, e.g. "id1, id2". Ids (not file names), because
    // two different photos can have the same name – the UI compares both lists: new id = added, missing id = removed.
    // Max 5 × 36 chars, fits the 500-char column.
    private string FormatImages()
        => string.Join(", ", GetImagesInOrder().Select(i => i.Id.Value.ToString()));
}
