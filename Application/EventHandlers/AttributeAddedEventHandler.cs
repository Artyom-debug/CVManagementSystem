using Application.Interfaces;
using Domain.Events;
using MediatR;

namespace Application.EventHandlers;

public sealed class AttributeAddedEventHandler : INotificationHandler<AttributeAddedEvent>
{
    private readonly ICacheService _cache;

    public AttributeAddedEventHandler(ICacheService cache)
    {
        _cache = cache;
    }

    public Task Handle(AttributeAddedEvent notification, CancellationToken cancellationToken)
    {
        return _cache.RemoveDependenciesAsync("attribute-library", cancellationToken);
    }
}
