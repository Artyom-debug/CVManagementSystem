using Domain.Abstractions;
using Domain.Enums;

namespace Domain.Entities;

public sealed class CV : BaseEntity
{
    public Status Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public bool IsRemovedFromProfile { get; private set; }

    public Guid ProfileId { get; private set; }

    public Profile? Profile { get; private set; }

    public Guid PositionId { get; private set; }

    public Position? Position { get; private set; }

    private readonly List<Like> _likes = new();

    public IReadOnlyCollection<Like> Likes => _likes.AsReadOnly();

    public CV(Guid profileId, Guid positionId)
    {
        if (profileId == Guid.Empty)
            throw new ArgumentException("Profile id cannot be empty", nameof(profileId));
        if (positionId == Guid.Empty)
            throw new ArgumentException("Position id cannot be empty", nameof(positionId));

        Status = Status.Draft;
        CreatedAt = DateTime.UtcNow;
        IsRemovedFromProfile = false;
        ProfileId = profileId;
        PositionId = positionId;
    }

    public void Publish()
    {

        if (Status == Status.Published)
            throw new InvalidOperationException("This CV has already published");
        if (IsRemovedFromProfile)
            throw new InvalidOperationException("Cannot publish a CV removed from the profile");

        Status = Status.Published;
        PublishedAt = DateTime.UtcNow;
    }

    public void RemoveFromProfile()
    {
        if (IsRemovedFromProfile)
            throw new InvalidOperationException("This CV has already been removed from the profile");

        IsRemovedFromProfile = true;
    }

    public void RestoreToProfile()
    {
        if (!IsRemovedFromProfile)
            return;

        IsRemovedFromProfile = false;
    }

    public void AddLike(string recruterId)
    {
        if (string.IsNullOrWhiteSpace(recruterId))
            throw new ArgumentException("Recruiter id cannot be empty", nameof(recruterId));
        if (_likes.Any(l => l.RecruterId == recruterId))
            throw new InvalidOperationException("You have already liked this CV");

        var newLike = new Like(Id, recruterId);
        _likes.Add(newLike);
    }

    public void RemoveLike(string recruterId)
    {
        if (string.IsNullOrWhiteSpace(recruterId))
            throw new ArgumentException("Recruiter id cannot be empty", nameof(recruterId));

        var like = _likes.FirstOrDefault(l => l.RecruterId == recruterId);
        if (like == null)
            throw new InvalidOperationException("You can't remove like");

        _likes.Remove(like);
    }
}
