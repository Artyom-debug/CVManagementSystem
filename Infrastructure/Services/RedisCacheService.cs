using Application.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace Infrastructure.Services;

public sealed class RedisCacheService : ICacheService
{

    private readonly IDatabase _database;

    public RedisCacheService(IConnectionMultiplexer connection)
    {
        _database = connection.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be empty.", nameof(key)); ;
        token.ThrowIfCancellationRequested();

        var json = await _database
            .StringGetAsync(key)
            .WaitAsync(token);

        if (json.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(json.ToString());
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken token, IEnumerable<string>? dependencies = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be empty.", nameof(key)); ;
        ArgumentNullException.ThrowIfNull(value);

        if (expiration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiration), "Expiration must be greater than zero.");
        }

        token.ThrowIfCancellationRequested();

        var json = JsonSerializer.Serialize(value);
        var saved = await _database
            .StringSetAsync(key, json, expiration)
            .WaitAsync(token);

        if (!saved)
            throw new InvalidOperationException($"Could not write cache key '{key}'.");

        if (dependencies is null)
            return;

        var dependencyNames = dependencies
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (dependencyNames.Length == 0)
            return;

        try
        {
            var tasks = dependencyNames.Select(async dependency =>
            {
                var dependencyKey = GetDependencyKey(dependency);

                await _database.SetAddAsync(dependencyKey, key);

                await _database.KeyExpireAsync(dependencyKey, TimeSpan.FromDays(1));
            });

            await Task.WhenAll(tasks).WaitAsync(token);
        }
        catch
        {
            await _database.KeyDeleteAsync(key);
            throw;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Cache key cannot be empty.", nameof(key)); ;
        token.ThrowIfCancellationRequested();

        await _database
            .KeyDeleteAsync(key)
            .WaitAsync(token);
    }

    public async Task RemoveDependenciesAsync(string dependency, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(dependency))
        {
            throw new ArgumentException("Cache dependency cannot be empty.", nameof(dependency));
        }

        token.ThrowIfCancellationRequested();

        var dependencyKey = GetDependencyKey(dependency);
        var cacheKeys = await _database
            .SetMembersAsync(dependencyKey)
            .WaitAsync(token);

        if (cacheKeys.Length > 0)
        {
            var redisKeys = cacheKeys
                .Where(value => !value.IsNullOrEmpty)
                .Select(value => (RedisKey)value.ToString())
                .ToArray();

            if (redisKeys.Length > 0)
            {
                await _database
                    .KeyDeleteAsync(redisKeys)
                    .WaitAsync(token);
            }
        }

        await _database
            .KeyDeleteAsync(dependencyKey)
            .WaitAsync(token);
    }

    private static string GetDependencyKey(string dependency) =>
        $"cache:dependency:{dependency}";
}
