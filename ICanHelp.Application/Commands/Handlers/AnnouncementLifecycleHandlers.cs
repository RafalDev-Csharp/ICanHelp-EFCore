using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

// Proste handlery: wczytaj agregat, wywołaj metodę domenową, zapisz. Reguły (kto, w jakim statusie) są w Announcement.

internal sealed class CancelAnnouncementHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork)
    : ICommandHandler<CancelAnnouncement>
{
    public Task HandleAsync(CancelAnnouncement command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.Cancel(new UserId(command.ActorId));
        }, cancellationToken);
}

internal sealed class StartAnnouncementHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<StartAnnouncement>
{
    public Task HandleAsync(StartAnnouncement command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.Start(new UserId(command.ActorId), clock.UtcNow);
        }, cancellationToken);
}

internal sealed class ReportAnnouncementDoneHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<ReportAnnouncementDone>
{
    public Task HandleAsync(ReportAnnouncementDone command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.ReportDone(new UserId(command.ActorId), clock.UtcNow);
        }, cancellationToken);
}

internal sealed class CompleteAnnouncementHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CompleteAnnouncement>
{
    public Task HandleAsync(CompleteAnnouncement command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.Complete(new UserId(command.ActorId), clock.UtcNow);
        }, cancellationToken);
}

/// <summary>F11 E: „Zrezygnuj” (Accepted → Open). Reguły (kto, status, powód) – <c>Announcement.Abandon</c>.</summary>
internal sealed class AbandonJobHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<AbandonJob>
{
    public async Task HandleAsync(AbandonJob command, CancellationToken cancellationToken = default)
    {
        var note = command.ToNote();

        await unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.Abandon(new UserId(command.ActorId), command.Reason, note, clock.UtcNow);
        }, cancellationToken);
    }
}

/// <summary>F11 E: „Przerwij zlecenie” (InProgress → Interrupted). Reguły – <c>Announcement.Interrupt</c>.</summary>
internal sealed class InterruptJobHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<InterruptJob>
{
    public async Task HandleAsync(InterruptJob command, CancellationToken cancellationToken = default)
    {
        var note = command.ToNote();

        await unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.Interrupt(new UserId(command.ActorId), command.Reason, note, clock.UtcNow);
        }, cancellationToken);
    }
}

/// <summary>F11 G: stanowisko drugiej strony (reguły – <c>Announcement.RespondToJobExit</c>).</summary>
internal sealed class RespondToJobExitHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<RespondToJobExit>
{
    public async Task HandleAsync(RespondToJobExit command, CancellationToken cancellationToken = default)
    {
        var text = new Description(command.Text.Trim());

        await unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.RespondToJobExit(command.Number, new UserId(command.ActorId), text, clock.UtcNow);
        }, cancellationToken);
    }
}

internal static class JobExitMapping
{
    /// <summary>
    /// Opis powodu: pusty / same spacje = brak, inaczej przycięty tekst (walidacja długości – <see cref="Description"/>).
    /// </summary>
    public static Description? ToNote(this JobExitCommand command) => command.Note?.Trim().ToOptionalDescription();
}
