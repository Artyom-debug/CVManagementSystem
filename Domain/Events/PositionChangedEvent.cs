using Domain.Abstractions;

namespace Domain.Events;

public sealed class PositionChangedEvent : BaseEvent
{
    public Guid PositionId { get; }

    public PositionChangedEvent(Guid positionId)
    {
        PositionId = positionId;
    }
}
