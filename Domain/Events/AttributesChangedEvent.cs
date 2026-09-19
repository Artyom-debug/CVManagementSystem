using Domain.Abstractions;
namespace Domain.Events;

public sealed class AttributesChangedEvent : BaseEvent
{
    public IReadOnlyCollection<Guid> AttributeIds { get; }

    public AttributesChangedEvent(IReadOnlyCollection<Guid> attributeIds)
    {
        AttributeIds = attributeIds;
    }
}
