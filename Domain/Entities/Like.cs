using Domain.Abstractions;

namespace Domain.Entities;

public sealed class Like : BaseEntity
{
    public Guid CVId { get; private set; }

    public string RecruterId { get; private set; }

    public Like(Guid CVId, string recruterId)
    {
        if (CVId == Guid.Empty)
            throw new ArgumentException("CV id cannot be empty", nameof(CVId));
        if (string.IsNullOrWhiteSpace(recruterId))
            throw new ArgumentException("Recruiter id cannot be empty", nameof(recruterId));

        this.CVId = CVId;
        this.RecruterId = recruterId;
    }
}
