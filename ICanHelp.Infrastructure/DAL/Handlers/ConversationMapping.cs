using ICanHelp.Application.DTO;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Infrastructure.DAL.Handlers;

/// <summary>A conversation with its timeline → DTOs.</summary>
internal static class ConversationMapping
{
    /// <summary>
    /// The conversation as seen by <paramref name="readerId"/> (a participant). Timeline: messages, offers of this helper,
    /// his job exits and edits of the announcement made after the conversation started – oldest first.
    /// </summary>
    public static ConversationDto AsDto(this Conversation entity, Announcement announcement, UserId readerId)
        => new()
        {
            Id = entity.Id,
            AnnouncementId = entity.AnnouncementId,
            AnnouncementTitle = announcement.Title,
            AnnouncementStatus = announcement.Status.ToString(),
            AnnouncementRevision = announcement.Revision,
            AnnouncementDoneReportedAt = announcement.DoneReportedAt,
            ClientId = entity.ClientId,
            HelperId = entity.HelperId,
            MyRole = entity.GetParty(readerId).ToString(),
            CreatedAt = entity.CreatedAt,
            ClientLastReadAt = entity.ClientLastReadAt,
            HelperLastReadAt = entity.HelperLastReadAt,
            UnreadCount = entity.CountUnread(readerId),
            CanWrite = announcement.AllowsConversationWith(entity.HelperId),
            Timeline = BuildTimeline(entity, announcement)
        };

    public static MessageDto AsDto(this ConversationMessage entity, Conversation conversation)
        => new()
        {
            Id = entity.Id,
            SenderId = entity.SenderId,
            SenderRole = conversation.GetParty(entity.SenderId).ToString(),
            Text = entity.Text,
            SentAt = entity.SentAt,
            AnnouncementRevision = entity.AnnouncementRevision
        };

    private static List<ConversationEntryDto> BuildTimeline(Conversation conversation, Announcement announcement)
    {
        var messages = conversation.Messages.Select(m => new ConversationEntryDto
        {
            Type = ConversationEntryDto.MessageType,
            At = m.SentAt,
            Message = m.AsDto(conversation)
        });

        var offers = announcement.Offers
            .Where(o => o.HelperId == conversation.HelperId)
            .Select(o => new ConversationEntryDto
            {
                Type = ConversationEntryDto.OfferType,
                At = o.CreatedAt,
                Offer = o.AsDto(announcement)
            });

        var changes = announcement.Changes
            .Where(c => c.ChangedAt > conversation.CreatedAt)
            .Select(c => new ConversationEntryDto
            {
                Type = ConversationEntryDto.AnnouncementChangedType,
                At = c.ChangedAt,
                Change = c.AsDto()
            });

        var exits = announcement.JobExits
            .Where(e => e.HelperId == conversation.HelperId)
            .Select(e => new ConversationEntryDto
            {
                Type = ConversationEntryDto.JobExitType,
                At = e.OccurredAt,
                JobExit = e.AsDto()
            });

        // Przy tym samym czasie: najpierw zmiana ogłoszenia, potem oferta, potem wiadomość (przyczyna przed reakcją).
        return changes.Concat(offers).Concat(exits).Concat(messages)
            .OrderBy(e => e.At)
            .ThenBy(e => e.Type switch
            {
                ConversationEntryDto.AnnouncementChangedType => 0,
                ConversationEntryDto.OfferType => 1,
                ConversationEntryDto.JobExitType => 2,
                _ => 3
            })
            .ToList();
    }
}
