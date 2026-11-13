using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

// Proste handlery: wczytaj agregat, wywołaj metodę domenową, zapisz. Reguły (kto, w jakim statusie) są w Announcement.

internal sealed class CancelAnnouncementHandler : ICommandHandler<CancelAnnouncement>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAnnouncementHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(CancelAnnouncement command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.Cancel(new UserId(command.ActorId));
        }, cancellationToken);
}

internal sealed class StartAnnouncementHandler : ICommandHandler<StartAnnouncement>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public StartAnnouncementHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(StartAnnouncement command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.Start(new UserId(command.ActorId), _clock.UtcNow);
        }, cancellationToken);
}

internal sealed class ReportAnnouncementDoneHandler : ICommandHandler<ReportAnnouncementDone>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ReportAnnouncementDoneHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(ReportAnnouncementDone command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.ReportDone(new UserId(command.ActorId), _clock.UtcNow);
        }, cancellationToken);
}

internal sealed class CompleteAnnouncementHandler : ICommandHandler<CompleteAnnouncement>
{
    private readonly IAnnouncementRepository _announcements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CompleteAnnouncementHandler(IAnnouncementRepository announcements, IUnitOfWork unitOfWork, IClock clock)
    {
        _announcements = announcements;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(CompleteAnnouncement command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var announcement = await _announcements.GetOrThrowAsync(command.AnnouncementId, cancellationToken);
            announcement.Complete(new UserId(command.ActorId), _clock.UtcNow);
        }, cancellationToken);
}
