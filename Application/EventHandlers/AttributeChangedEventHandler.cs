using Application.Interfaces;
using Domain.Events;
using MediatR;

namespace Application.EventHandlers;

public sealed class AttributeChangedEventHandler
    : INotificationHandler<AttributesChangedEvent>
{
    private readonly ICacheService _cache;

    public AttributeChangedEventHandler(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task Handle(
        AttributesChangedEvent notification,
        CancellationToken cancellationToken)
    {
        await _cache.RemoveDependenciesAsync(
            "attribute-library",
            cancellationToken);

        foreach (var attributeId in notification.AttributeIds)
        {
            await _cache.RemoveDependenciesAsync(
                $"attribute:{attributeId}",
                cancellationToken);
        }
    }
}
