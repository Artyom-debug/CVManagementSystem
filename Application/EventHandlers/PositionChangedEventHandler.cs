using Application.Interfaces;
using Domain.Events;
using MediatR;

namespace Application.EventHandlers;

public sealed class PositionChangedEventHandler
    : INotificationHandler<PositionChangedEvent>
{
    private readonly ICacheService _cache;

    public PositionChangedEventHandler(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task Handle(
        PositionChangedEvent notification,
        CancellationToken cancellationToken)
    {
        await _cache.RemoveDependenciesAsync(
            $"position:{notification.PositionId}",
            cancellationToken);

        await _cache.RemoveDependenciesAsync(
            "position-library",
            cancellationToken);
    }
}
