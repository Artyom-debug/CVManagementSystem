using Application.Common.Security;
using Application.Dtos;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Integrations;

public sealed record RedeemOdooPositionStatisticsCommand(string Token) : IRequest<OdooPositionStatisticsDto>;

internal sealed class RedeemOdooPositionStatisticsCommandHandler : IRequestHandler<RedeemOdooPositionStatisticsCommand, OdooPositionStatisticsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public RedeemOdooPositionStatisticsCommandHandler(IApplicationDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<OdooPositionStatisticsDto> Handle(RedeemOdooPositionStatisticsCommand request, CancellationToken cancellationToken)
    {
        if (!PositionApiTokens.IsValidFormat(request.Token))
            throw new UnauthorizedAccessException("Invalid position API token.");

        var cacheKey = PositionApiTokens.CacheKey(PositionApiTokens.Hash(request.Token));
        var positionId = await _cache.GetAsync<Guid?>(cacheKey, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid or expired position API token.");

        var position = await _context.Positions
            .AsNoTracking()
            .Include(item => item.PositionAttributes)
                .ThenInclude(item => item.Attribute)
            .SingleOrDefaultAsync(item => item.Id == positionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("The position is no longer available.");

        var profileIds = await _context.CVs
            .AsNoTracking()
            .Where(cv => cv.PositionId == position.Id && cv.Status == Status.Published)
            .Select(cv => cv.ProfileId)
            .ToListAsync(cancellationToken);

        var attributeIds = position.PositionAttributes
            .Select(item => item.AttributeId)
            .ToArray();

        var values = profileIds.Count == 0 || attributeIds.Length == 0 ? []
            : await _context.ProfileAttributes
                .AsNoTracking()
                .Include(value => value.DropdownOption)
                .Where(value => profileIds.Contains(value.ProfileId) && attributeIds.Contains(value.AttributeId))
                .ToListAsync(cancellationToken);

        var valuesByAttribute = values.ToLookup(value => value.AttributeId);
        var statistics = position.PositionAttributes
            .OrderBy(item => item.DisplayOrder)
            .Select(item => Aggregate(item, valuesByAttribute[item.AttributeId]))
            .ToArray();

        var result = new OdooPositionStatisticsDto(
            position.Id,
            position.Name,
            position.Description,
            position.CreatedAt,
            profileIds.Count,
            statistics);

        var redeemedPositionId = await _cache.TakeAsync<Guid?>(cacheKey, cancellationToken);
        if (redeemedPositionId != position.Id)
            throw new UnauthorizedAccessException("Invalid or expired position API token.");

        return result;
    }

    private static OdooAttributeStatisticDto Aggregate(PositionAttribute positionAttribute, IEnumerable<ProfileAttributeValue> sourceValues)
    {
        var attribute = positionAttribute.Attribute!;
        var values = sourceValues.ToArray();
        var numbers = values
            .Where(value => value.NumericValue is double number && double.IsFinite(number))
            .Select(value => value.NumericValue!.Value)
            .ToArray();
        var dates = values
            .Where(value => value.DateValue.HasValue)
            .Select(value => value.DateValue!.Value)
            .ToArray();
        var periods = values
            .Where(value => value.PeriodValue is not null)
            .Select(value => value.PeriodValue!)
            .ToArray();

        var filledCount = attribute.Type switch
        {
            AttributeType.String => values.Count(value => !string.IsNullOrWhiteSpace(value.StringValue)),
            AttributeType.Text => values.Count(value => !string.IsNullOrWhiteSpace(value.TextValue)),
            AttributeType.Image => values.Count(value => !string.IsNullOrWhiteSpace(value.ImageValue)),
            AttributeType.Numeric => numbers.Length,
            AttributeType.Date => dates.Length,
            AttributeType.Period => periods.Length,
            AttributeType.Checkbox => values.Count(value => value.CheckboxValue.HasValue),
            AttributeType.Dropdown => values.Count(value => value.DropdownOptionId.HasValue),
            _ => 0
        };

        IReadOnlyList<OdooTopValueDto> topValues = attribute.Type switch
        {
            AttributeType.String when !attribute.IsSystem => GetTopValues(
                values.Select(value => value.StringValue)),
            AttributeType.Text when !attribute.IsSystem => GetTopValues(
                values.Select(value => value.TextValue)),
            AttributeType.Dropdown => GetTopValues(
                values.Select(value => value.DropdownOption?.Option)),
            _ => []
        };

        return new OdooAttributeStatisticDto(
            attribute.Id,
            attribute.Name,
            attribute.Type.ToString(),
            positionAttribute.DisplayOrder,
            filledCount,
            attribute.Type == AttributeType.Numeric && numbers.Length > 0 ? numbers.Min() : null,
            attribute.Type == AttributeType.Numeric && numbers.Length > 0 ? numbers.Max() : null,
            attribute.Type == AttributeType.Numeric && numbers.Length > 0 ? numbers.Average() : null,
            attribute.Type == AttributeType.Date && dates.Length > 0 ? dates.Min() : null,
            attribute.Type == AttributeType.Date && dates.Length > 0 ? dates.Max() : null,
            attribute.Type == AttributeType.Period && periods.Length > 0 ? periods.Min(period => period.Start) : null,
            attribute.Type == AttributeType.Period && periods.Any(period => period.End.HasValue)
                ? periods.Where(period => period.End.HasValue).Max(period => period.End)
                : null,
            attribute.Type == AttributeType.Checkbox ? values.Count(value => value.CheckboxValue == true) : 0,
            attribute.Type == AttributeType.Checkbox ? values.Count(value => value.CheckboxValue == false) : 0,
            topValues);
    }

    private static IReadOnlyList<OdooTopValueDto> GetTopValues(IEnumerable<string?> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(group => new OdooTopValueDto(group.Key[..Math.Min(group.Key.Length, 200)], group.Count()))
            .ToArray();
}
