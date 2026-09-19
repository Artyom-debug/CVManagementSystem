using Application.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Services;

public sealed class RecentAttributesCache : IRecentAttributesCache
{
    private static readonly TimeSpan Expiration = TimeSpan.FromDays(30);

    private readonly IDatabase _database;

    public RecentAttributesCache(IConnectionMultiplexer connection)
    {
        _database = connection.GetDatabase();
    }

    public async Task<IReadOnlyList<Guid>> GetAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty.", nameof(userId)); ;
        cancellationToken.ThrowIfCancellationRequested();

        var values = await _database
            .SortedSetRangeByRankAsync(GetKey(userId), 0, 29, Order.Descending)
            .WaitAsync(cancellationToken);

        return values
            .Select(value => Guid.TryParse(value.ToString(), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    public async Task AddAsync(string userId, Guid attributeId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty.", nameof(userId));

        if (attributeId == Guid.Empty)
            throw new ArgumentException("Attribute id cannot be empty.", nameof(attributeId));

        token.ThrowIfCancellationRequested();

        var key = GetKey(userId);
        var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _database
            .SortedSetAddAsync(key, attributeId.ToString(), score)
            .WaitAsync(token);

        await TrimAndSetExpirationAsync(key, token);
    }

    public async Task AddRangeAsync(string userId, IReadOnlyCollection<Guid> attributeIds, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        ArgumentNullException.ThrowIfNull(attributeIds);

        if (attributeIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Attribute id cannot be empty.", nameof(attributeIds));

        token.ThrowIfCancellationRequested();

        var ids = attributeIds
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
            return;

        var key = GetKey(userId);
        var score = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entries = ids
            .Select((id, index) => new SortedSetEntry(id.ToString(), score + (ids.Length - index) / 1000d))
            .ToArray();

        await _database
            .SortedSetAddAsync(key, entries)
            .WaitAsync(token);

        await TrimAndSetExpirationAsync(key, token);
    }

    private async Task TrimAndSetExpirationAsync(RedisKey key, CancellationToken token)
    {
        await _database
            .SortedSetRemoveRangeByRankAsync(key, 0, -31)
            .WaitAsync(token);

        await _database
            .KeyExpireAsync(key, Expiration)
            .WaitAsync(token);
    }

    private static string GetKey(string userId) =>
        $"recent:attributes:{userId}";
}
