using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Time;
using ICanHelp.Core.Entities.Alerts;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Commands.Handlers;

internal sealed class CreateAlertHandler(IAnnouncementAlertRepository alerts, ICategoryRepository categories,
    IUserRepository users, IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<CreateAlert>
{
    public Task HandleAsync(CreateAlert command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var now = clock.UtcNow;
            var subcategoryIds = AlertMapping.ToIds(command.SubcategoryIds);

            await users.GetActiveOrThrowAsync(command.UserId, cancellationToken);
            await categories.EnsureCategoryAsync(new Id(command.CategoryId), subcategoryIds, cancellationToken);
            AnnouncementAlert.EnsureCanCreate(await alerts.CountByUserAsync(userId, cancellationToken));

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

            await alerts.AddAsync(alert, cancellationToken);
        }, cancellationToken);
}

internal sealed class UpdateAlertHandler(IAnnouncementAlertRepository alerts, ICategoryRepository categories,
    IUnitOfWork unitOfWork, IClock clock) : ICommandHandler<UpdateAlert>
{
    public Task HandleAsync(UpdateAlert command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var now = clock.UtcNow;
            var alert = await alerts.GetOwnedOrThrowAsync(command.AlertId, userId, cancellationToken);

            var categoryChanged = command.CategoryId is { } newCategory && newCategory != alert.CategoryId.Value;
            var categoryId = new Id(command.CategoryId ?? alert.CategoryId.Value);

            // Nowa kategoria bez podanych podkategorii → cała kategoria (stare podkategorie do niej nie należą).
            var subcategoryIds = command.SubcategoryIds is not null
                ? AlertMapping.ToIds(command.SubcategoryIds)
                : categoryChanged ? Array.Empty<Id>() : alert.SubcategoryIds;

            if (categoryChanged || command.SubcategoryIds is not null)
            {
                await categories.EnsureCategoryAsync(categoryId, subcategoryIds, cancellationToken);
            }

            alert.Update(
                userId,
                command.Name is null ? alert.Name : new StringObject(command.Name),
                categoryId,
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

internal sealed class DeleteAlertHandler(IAnnouncementAlertRepository alerts, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteAlert>
{
    public Task HandleAsync(DeleteAlert command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var alert = await alerts.GetOwnedOrThrowAsync(command.AlertId, new UserId(command.UserId),
                cancellationToken);
            alerts.Remove(alert);
        }, cancellationToken);
}

internal sealed class SetAlertEnabledHandler(IAnnouncementAlertRepository alerts, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<SetAlertEnabled>
{
    public Task HandleAsync(SetAlertEnabled command, CancellationToken cancellationToken = default)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var userId = new UserId(command.UserId);
            var alert = await alerts.GetOwnedOrThrowAsync(command.AlertId, userId, cancellationToken);

            if (command.IsEnabled)
            {
                alert.Enable(userId, clock.UtcNow);
            }
            else
            {
                alert.Disable(userId, clock.UtcNow);
            }
        }, cancellationToken);
}

/// <summary>Dane alertu z komendy → listy id i punktów (obszar: jeden punkt, trasa: polilinia).</summary>
internal static class AlertMapping
{
    public static IReadOnlyList<Id> ToIds(IEnumerable<Guid>? ids)
        => (ids ?? []).Select(id => new Id(id)).ToList();

    public static IReadOnlyList<Location> ToPoints(AlertKind kind, LocationDto? center,
        IReadOnlyList<LocationDto>? routePoints)
        => kind switch
        {
            AlertKind.Area => center is null
                ? throw new InvalidAlertException("An area alert needs the centre (center).")
                : [center.ToLocation()],
            AlertKind.Route => routePoints is null
                ? throw new InvalidAlertException("A route alert needs the points of the route (routePoints).")
                : routePoints.Select(point => point.ToLocation()).ToList(),
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
}
