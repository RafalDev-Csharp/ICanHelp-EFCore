using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class CreateAlertHandler : ICommandHandler<CreateAlert>
{
    private readonly IAnnouncementAlertRepository _alerts;
    private readonly ICategoryRepository _categories;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateAlertHandler(IAnnouncementAlertRepository alerts, ICategoryRepository categories,
        IUserRepository users, IUnitOfWork unitOfWork, IClock clock)
    {
        _alerts = alerts;
        _categories = categories;
        _users = users;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(CreateAlert command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var now = _clock.UtcNow;
            var subcategoryIds = AlertMapping.ToIds(command.SubcategoryIds);

            await _users.GetActiveOrThrowAsync(command.UserId, cancellationToken);
            await _categories.EnsureCategoryAsync(command.CategoryId, subcategoryIds, cancellationToken);
            AnnouncementAlert.EnsureCanCreate(await _alerts.CountByUserAsync(userId, cancellationToken));

            var alert = new AnnouncementAlert(
                new Id(command.AlertId),
                userId,
                new StringObject(command.Name),
                command.Kind,
                new Id(command.CategoryId),
                subcategoryIds,
                AlertMapping.ToPoints(command.Kind, command.Center, command.RoutePoints),
                command.RadiusKm,
                now);

            if (command.Schedule is { } schedule)
            {
                alert.SetSchedule(userId, schedule.Days, schedule.From, schedule.To, now);
            }

            await _alerts.AddAsync(alert, cancellationToken);
        }, cancellationToken);
}

internal sealed class UpdateAlertHandler : ICommandHandler<UpdateAlert>
{
    private readonly IAnnouncementAlertRepository _alerts;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UpdateAlertHandler(IAnnouncementAlertRepository alerts, ICategoryRepository categories,
        IUnitOfWork unitOfWork, IClock clock)
    {
        _alerts = alerts;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(UpdateAlert command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var now = _clock.UtcNow;
            var alert = await _alerts.GetOwnedOrThrowAsync(command.AlertId, userId, cancellationToken);

            var categoryChanged = command.CategoryId is { } newCategory && newCategory != alert.CategoryId.Value;
            var categoryId = command.CategoryId ?? alert.CategoryId.Value;

            // Nowa kategoria bez podanych podkategorii → cała kategoria (stare podkategorie do niej nie należą).
            var subcategoryIds = command.SubcategoryIds is not null
                ? AlertMapping.ToIds(command.SubcategoryIds)
                : categoryChanged ? Array.Empty<Id>() : alert.SubcategoryIds;

            if (categoryChanged || command.SubcategoryIds is not null)
            {
                await _categories.EnsureCategoryAsync(categoryId, subcategoryIds, cancellationToken);
            }

            alert.Update(
                userId,
                command.Name is null ? alert.Name : new StringObject(command.Name),
                new Id(categoryId),
                subcategoryIds,
                AlertMapping.ToPointsForUpdate(alert, command.Center, command.RoutePoints),
                command.RadiusKm ?? alert.RadiusKm,
                now);

            if (command.Schedule is { } schedule)
            {
                alert.SetSchedule(userId, schedule.Days, schedule.From, schedule.To, now);
            }
        }, cancellationToken);
}

internal sealed class DeleteAlertHandler : ICommandHandler<DeleteAlert>
{
    private readonly IAnnouncementAlertRepository _alerts;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAlertHandler(IAnnouncementAlertRepository alerts, IUnitOfWork unitOfWork)
    {
        _alerts = alerts;
        _unitOfWork = unitOfWork;
    }

    public Task HandleAsync(DeleteAlert command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var alert = await _alerts.GetOwnedOrThrowAsync(command.AlertId, new UserId(command.UserId),
                cancellationToken);
            _alerts.Remove(alert);
        }, cancellationToken);
}

internal sealed class SetAlertEnabledHandler : ICommandHandler<SetAlertEnabled>
{
    private readonly IAnnouncementAlertRepository _alerts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SetAlertEnabledHandler(IAnnouncementAlertRepository alerts, IUnitOfWork unitOfWork, IClock clock)
    {
        _alerts = alerts;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public Task HandleAsync(SetAlertEnabled command, CancellationToken cancellationToken = default)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var alert = await _alerts.GetOwnedOrThrowAsync(command.AlertId, userId, cancellationToken);

            if (command.IsEnabled)
            {
                alert.Enable(userId, _clock.UtcNow);
            }
            else
            {
                alert.Disable(userId, _clock.UtcNow);
            }
        }, cancellationToken);
}

/// <summary>Wspólne kroki handlerów alertów.</summary>
internal static class AlertMapping
{
    /// <summary>Cudzy alert wygląda jak nieistniejący (404) – nie zdradzamy, że istnieje.</summary>
    public static async Task<AnnouncementAlert> GetOwnedOrThrowAsync(this IAnnouncementAlertRepository alerts,
        Guid alertId, UserId userId, CancellationToken cancellationToken)
    {
        var alert = await alerts.GetByIdAsync(new Id(alertId), cancellationToken);
        if (alert is null || !alert.IsOwnedBy(userId))
        {
            throw new AlertNotFoundException(alertId);
        }

        return alert;
    }

    /// <summary>Kategoria musi istnieć, a każda podkategoria do niej należeć (inaczej 400 InvalidCategory).</summary>
    public static async Task EnsureCategoryAsync(this ICategoryRepository categories, Guid categoryId,
        IReadOnlyCollection<Id> subcategoryIds, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(new Id(categoryId), cancellationToken);
        if (category is null)
        {
            throw new InvalidCategoryException(categoryId, subcategoryIds.FirstOrDefault()?.Value ?? Guid.Empty);
        }

        var wrong = subcategoryIds.FirstOrDefault(s => !category.HasSubcategory(s));
        if (wrong is not null)
        {
            throw new InvalidCategoryException(categoryId, wrong.Value);
        }
    }

    public static IReadOnlyList<Id> ToIds(IEnumerable<Guid>? ids)
        => (ids ?? []).Select(id => new Id(id)).ToList();

    public static IReadOnlyList<Location> ToPoints(AlertKind kind, LocationDto? center,
        IReadOnlyList<LocationDto>? routePoints)
        => kind switch
        {
            AlertKind.Area => center is null
                ? throw new InvalidAlertException("An area alert needs the centre (center).")
                : new List<Location> { ToLocation(center) },
            AlertKind.Route => routePoints is null
                ? throw new InvalidAlertException("A route alert needs the points of the route (routePoints).")
                : routePoints.Select(ToLocation).ToList(),
            _ => throw new InvalidAlertException($"Alert kind: {kind} is invalid.")
        };

    /// <summary>PATCH: nowe punkty tylko dla właściwego rodzaju; nic nie podano = bez zmian.</summary>
    public static IReadOnlyList<Location> ToPointsForUpdate(AnnouncementAlert alert, LocationDto? center,
        IReadOnlyList<LocationDto>? routePoints)
    {
        if (alert.Kind == AlertKind.Area && routePoints is not null)
        {
            throw new InvalidAlertException("An area alert has no route – the kind of an alert cannot change.");
        }

        if (alert.Kind == AlertKind.Route && center is not null)
        {
            throw new InvalidAlertException("A route alert has no centre – the kind of an alert cannot change.");
        }

        return center is null && routePoints is null
            ? alert.Points
            : ToPoints(alert.Kind, center, routePoints);
    }

    private static Location ToLocation(LocationDto location) => new(location.Latitude, location.Longitude);
}
