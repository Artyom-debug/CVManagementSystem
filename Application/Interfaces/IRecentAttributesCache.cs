namespace Application.Interfaces;

public interface IRecentAttributesCache
{
    Task<IReadOnlyList<Guid>> GetAsync(string userId, CancellationToken cancellationToken);

    Task AddAsync(string userId, Guid attributeId, CancellationToken token);

    Task AddRangeAsync(string userId, IReadOnlyCollection<Guid> attributeIds, CancellationToken token);
}
