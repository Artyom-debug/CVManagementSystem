using Domain.Abstractions;

namespace Domain.Events;

public sealed class ProfileChangedEvent : BaseEvent
{
    public Guid ProfileId { get; }

    public ProfileChangedEvent(Guid profileId)
    {
        ProfileId = profileId;
    }
}
