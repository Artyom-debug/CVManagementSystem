using System.Reflection;
using System.Text.Json;
using Application.Dtos;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Services;
using StackExchange.Redis;
using Attribute = Domain.Entities.Attribute;

var securityType = typeof(OdooPositionStatisticsDto).Assembly.GetType(
    "Application.Common.Security.PositionApiTokens")!;
var generate = securityType.GetMethod("Generate", BindingFlags.Static | BindingFlags.Public)!;
var isValidFormat = securityType.GetMethod("IsValidFormat", BindingFlags.Static | BindingFlags.Public)!;
var hash = securityType.GetMethod("Hash", BindingFlags.Static | BindingFlags.Public)!;
var cacheKey = securityType.GetMethod("CacheKey", BindingFlags.Static | BindingFlags.Public)!;

var firstToken = (string)generate.Invoke(null, null)!;
var secondToken = (string)generate.Invoke(null, null)!;
Check(firstToken != secondToken, "Tokens must be random.");
Check(firstToken.Length == 69 && firstToken.StartsWith("odoo_"), "Unexpected token format.");
Check((bool)isValidFormat.Invoke(null, [firstToken])!, "Generated token must be valid.");
Check(!(bool)isValidFormat.Invoke(null, ["invalid"])!, "Invalid token must be rejected.");
Check(((string)hash.Invoke(null, [firstToken])!).Length == 64, "Unexpected hash format.");

var position = new Position("Developer", "Description", 3);
Check(typeof(Position).GetProperty("OdooApiTokenHash") is null,
    "Position must not persist the one-time token hash in PostgreSQL.");

var redis = DispatchProxy.Create<IConnectionMultiplexer, RedisConnectionProxy>();
var cache = new RedisCacheService(redis);
var firstHash = (string)hash.Invoke(null, [firstToken])!;
var key = (string)cacheKey.Invoke(null, [firstHash])!;
await cache.SetAsync(key, position.Id, TimeSpan.FromHours(24), default);
Check(await cache.GetAsync<Guid?>(key, default) == position.Id,
    "Stored token must resolve to its position.");
Check(await cache.TakeAsync<Guid?>(key, default) == position.Id,
    "First redemption must succeed.");
Check(await cache.TakeAsync<Guid?>(key, default) is null,
    "A token must not be redeemable twice.");

var aggregateType = typeof(OdooPositionStatisticsDto).Assembly.GetType(
    "Application.Commands.Integrations.RedeemOdooPositionStatisticsCommandHandler")!;
var aggregate = aggregateType.GetMethod("Aggregate", BindingFlags.Static | BindingFlags.NonPublic)!;

var numericAttribute = new Attribute("Experience", null, AttributeType.Numeric, Category.Experience, false);
var numericPositionAttribute = Link(position, numericAttribute);
var numericValues = new[]
{
    Value(numericAttribute, 2.0),
    Value(numericAttribute, 4.0),
};
var numericStatistics = (OdooAttributeStatisticDto)aggregate.Invoke(null, [numericPositionAttribute, numericValues])!;
Check(numericStatistics.FilledCount == 2 && numericStatistics.NumericMinimum == 2 &&
      numericStatistics.NumericMaximum == 4 && numericStatistics.NumericAverage == 3,
    "Numeric statistics are incorrect.");

var systemAttribute = new Attribute("Email", null, AttributeType.String, Category.PersonalInformation, true);
var systemStatistics = (OdooAttributeStatisticDto)aggregate.Invoke(null, [
    Link(position, systemAttribute),
    new[] { Value(systemAttribute, "private@example.test"), Value(systemAttribute, "private@example.test") }
])!;
Check(systemStatistics.FilledCount == 2 && systemStatistics.TopValues.Count == 0,
    "System attribute values must not be exported as popular values.");

var response = new OdooPositionStatisticsDto(
    position.Id, position.Name, position.Description, position.CreatedAt, 2, [numericStatistics]);
var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
using var document = JsonDocument.Parse(json);
var root = document.RootElement;
Check(root.GetProperty("positionId").GetGuid() == position.Id &&
      root.GetProperty("publishedCvCount").GetInt32() == 2 &&
      root.GetProperty("attributes")[0].GetProperty("type").GetString() == "Numeric" &&
      root.GetProperty("attributes")[0].GetProperty("numericAverage").GetDouble() == 3,
    "API JSON contract does not match the Odoo importer.");

Console.WriteLine("PASS: one-time Redis redemption, numeric aggregation, system-value privacy, Odoo JSON contract.");

static PositionAttribute Link(Position position, Attribute attribute)
{
    var link = new PositionAttribute(position.Id, attribute.Id, 0);
    typeof(PositionAttribute).GetProperty(nameof(PositionAttribute.Attribute))!.SetValue(link, attribute);
    return link;
}

static ProfileAttributeValue Value(Attribute attribute, object value)
{
    var result = new ProfileAttributeValue(Guid.NewGuid(), attribute.Id, 0);
    result.UpdateAttributeValue(value, attribute.Type);
    return result;
}

static void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

public class RedisConnectionProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        method?.Name == "GetDatabase"
            ? DispatchProxy.Create<IDatabase, RedisDatabaseProxy>()
            : throw new Exception("Unexpected Redis call: " + method?.Name);
}

public class RedisDatabaseProxy : DispatchProxy
{
    private readonly Dictionary<string, RedisValue> _values = new();

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        var key = args![0]!.ToString()!;
        return method?.Name switch
        {
            "StringSetAsync" => Task.FromResult(_values.TryAdd(key, (RedisValue)args[1]!)),
            "StringGetAsync" => Task.FromResult(_values.TryGetValue(key, out var value) ? value : RedisValue.Null),
            "StringGetDeleteAsync" => Task.FromResult(Take(key)),
            _ => throw new Exception("Unexpected Redis call: " + method?.Name)
        };
    }

    private RedisValue Take(string key)
    {
        if (!_values.Remove(key, out var value))
            return RedisValue.Null;
        return value;
    }
}
