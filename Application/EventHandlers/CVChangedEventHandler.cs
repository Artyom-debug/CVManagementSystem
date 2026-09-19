using Application.Interfaces;
using Domain.Events;
using MediatR;

namespace Application.EventHandlers;

public sealed class CVChangedEventHandler : INotificationHandler<CVChangedEvent>
{
    private readonly ICacheService _cache;

    public CVChangedEventHandler(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task Handle(CVChangedEvent notification, CancellationToken cancellationToken)
    {
        await _cache.RemoveDependenciesAsync($"cv:{notification.CVId}", cancellationToken);

        await _cache.RemoveDependenciesAsync($"profile:{notification.ProfileId}", cancellationToken);

        await _cache.RemoveDependenciesAsync($"position:{notification.PositionId}", cancellationToken);
    }
}
