using ICanHelp.Core.Enums;

namespace ICanHelp.Core.Entities;

/// <summary>
/// One field changed in one edit: the value before and after, as text, so the UI can highlight the difference
/// (e.g. a word diff of the description). Formats: Budget "150.00 PLN", Location "50.0614, 19.9366",
/// Category "{categoryId}/{subcategoryId}", Difficulty "Easy". Part of <see cref="AnnouncementChange"/>.
/// </summary>
public class AnnouncementFieldChange
{
    /// <summary>Exactly one flag of <see cref="AnnouncementFields"/>.</summary>
    public AnnouncementFields Field { get; private set; }

    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }

    // Required by EF Core.
    private AnnouncementFieldChange()
    {
    }

    internal AnnouncementFieldChange(AnnouncementFields field, string? oldValue, string? newValue)
    {
        Field = field;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
