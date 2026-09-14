namespace Application.Interfaces;

public interface IRecentAttributesCache
{
    Task<IReadOnlyList<Guid>> GetAsync(string userId, CancellationToken cancellationToken);
}
