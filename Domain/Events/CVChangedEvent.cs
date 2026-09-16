using Domain.Abstractions;

namespace Domain.Events;

public sealed class CVChangedEvent : BaseEvent
{
    public Guid CVId { get; }

    public Guid ProfileId { get; }

    public Guid PositionId { get; }

    public CVChangedEvent(
        Guid cvId,
        Guid profileId,
        Guid positionId)
    {
        CVId = cvId;
        ProfileId = profileId;
        PositionId = positionId;
    }
}
