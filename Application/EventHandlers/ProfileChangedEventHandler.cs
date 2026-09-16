using Application.Interfaces;
using Domain.Events;
using MediatR;

namespace Application.EventHandlers;

public sealed class ProfileChangedEventHandler
    : INotificationHandler<ProfileChangedEvent>
{
    private readonly ICacheService _cache;

    public ProfileChangedEventHandler(ICacheService cache)
    {
        _cache = cache;
    }

    public Task Handle(
        ProfileChangedEvent notification,
        CancellationToken cancellationToken)
    {
        return _cache.RemoveDependenciesAsync(
            $"profile:{notification.ProfileId}",
            cancellationToken);
    }
}
