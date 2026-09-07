using Domain.Abstractions;

namespace Domain.Entities;

public sealed class DiscussionPost : BaseEntity
{
    public Guid PositionId { get; private set; }

    public Position? Position { get; private set; }

    public string AuthorId { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    private DiscussionPost()
    {
    }

    internal DiscussionPost(Guid positionId, string authorId, string content)
    {
        if (positionId == Guid.Empty)
            throw new ArgumentException("Position id cannot be empty", nameof(positionId));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author id cannot be empty", nameof(authorId));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Discussion post content cannot be empty", nameof(content));

        PositionId = positionId;
        AuthorId = authorId;
        Content = content;
        CreatedAt = DateTime.UtcNow;
    }
}
