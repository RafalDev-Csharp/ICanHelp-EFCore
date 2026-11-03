using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

/// <summary>
/// A rating one participant of a finished announcement gives to the other one.
/// One rater can rate an announcement only once (enforced by a unique index and the application layer).
/// </summary>
public class Rating
{
    public Id Id { get; private set; }
    public Id AnnouncementId { get; private set; }

    public UserId RaterId { get; private set; }
    public UserId RateeId { get; private set; }

    public Rate Rate { get; private set; }
    public Description? Comment { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Required by EF Core.
    private Rating()
    {
    }

    public Rating(Id id, Id announcementId, UserId raterId, UserId rateeId, Rate rate,
        Description? comment, DateTime createdAtUtc)
    {
        if (raterId == rateeId)
        {
            throw new DomainRuleException("A user cannot rate himself.");
        }

        Id = id;
        AnnouncementId = announcementId;
        RaterId = raterId;
        RateeId = rateeId;
        Rate = rate;
        Comment = comment;
        CreatedAt = createdAtUtc;
    }

    public void Update(Rate rate, Description? comment)
    {
        Rate = rate;
        Comment = comment;
    }
}
