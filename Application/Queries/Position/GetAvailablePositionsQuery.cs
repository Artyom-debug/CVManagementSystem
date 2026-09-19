using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Dtos;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Value_Objects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Position;

public sealed record GetAvailablePositionsQuery(int Page = 1, int PageSize = 30) : IRequest<PageResult<PositionDto>>;

public sealed class GetAvailablePositionsQueryValidator : AbstractValidator<GetAvailablePositionsQuery>
{
    public GetAvailablePositionsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithName(nameof(GetAvailablePositionsQuery.Page))
            .WithMessage("Requested page is outside the supported range.");
    }
}

internal sealed class GetAvailablePositionsQueryHandler : IRequestHandler<GetAvailablePositionsQuery, PageResult<PositionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ICacheService _cache;

    public GetAvailablePositionsQueryHandler(IApplicationDbContext context, IUser user, ICacheService cache)
    {
        _context = context;
        _user = user;
        _cache = cache;
    }

    public async Task<PageResult<PositionDto>> Handle(GetAvailablePositionsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var cacheKey = $"positions:available:v1:user:{_user.Id}:page:{request.Page}:size:{request.PageSize}";
        var cachedResult = await _cache.GetAsync<PageResult<PositionDto>>(cacheKey, cancellationToken);

        if (cachedResult is not null)
            return cachedResult;

        var profile = await _context.Profiles
            .AsNoTracking()
            .Include(profile => profile.AttributeValues)
            .SingleOrDefaultAsync(profile => profile.UserId == _user.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Profile), _user.Id);

        var profileValues = profile.AttributeValues
            .ToDictionary(value => value.AttributeId);

        var positionsToSkip = (request.Page - 1) * request.PageSize;
        var skippedPositions = 0;
        var scannedPositions = 0;
        var batchSize = Math.Max(request.PageSize * 4, 100);
        var page = new List<Domain.Entities.Position>(request.PageSize + 1);

        while (page.Count <= request.PageSize)
        {
            var batch = await _context.Positions
                .AsNoTracking()
                .AsSplitQuery()
                .Include(position => position.Tags)
                .Include(position => position.AccessRules)
                .OrderBy(position => position.Name)
                .ThenBy(position => position.Id)
                .Skip(scannedPositions)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            scannedPositions += batch.Count;

            foreach (var position in batch)
            {
                if (!CanAccess(position, profileValues))
                    continue;

                if (skippedPositions < positionsToSkip)
                {
                    skippedPositions++;
                    continue;
                }

                page.Add(position);
                if (page.Count > request.PageSize)
                    break;
            }

            if (page.Count > request.PageSize || batch.Count < batchSize)
                break;
        }

        var hasNextPage = page.Count > request.PageSize;
        if (hasNextPage)
            page.RemoveAt(page.Count - 1);

        var items = page
            .Select(position => new PositionDto(position.Id, position.Version, position.Name, position.Description, position.MaxProjectCount, position.IsPublic, position.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToList()))
            .ToList();

        var result = new PageResult<PositionDto>(items, request.Page, request.PageSize, hasNextPage);

        await _cache.SetAsync(cacheKey, result, request.Page <= 3 ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(2), cancellationToken, ["position-library", $"profile:{profile.Id}"]);

        return result;
    }

    private static bool CanAccess(Domain.Entities.Position position, IReadOnlyDictionary<Guid, ProfileAttributeValue> profileValues)
    {
        if (position.IsPublic)
            return true;

        return position.AccessRules.Count > 0 &&
               position.AccessRules.All(rule => profileValues.TryGetValue(rule.AttributeId, out var value) && Matches(value, rule));
    }

    private static bool Matches(ProfileAttributeValue value, AccessRule rule)
    {
        return rule.AttributeType switch
        {
            AttributeType.String => CompareStrings(value.StringValue, rule),
            AttributeType.Text => CompareStrings(value.TextValue, rule),
            AttributeType.Image => CompareStrings(value.ImageValue, rule),
            AttributeType.Numeric when value.NumericValue.HasValue =>
                Compare(value.NumericValue.Value.CompareTo(rule.Value.NumericValue!.Value), rule.Operator),
            AttributeType.Date when value.DateValue.HasValue =>
                Compare(value.DateValue.Value.CompareTo(rule.Value.DateValue!.Value), rule.Operator),
            AttributeType.Period when value.PeriodValue is not null =>
                CompareEquality(value.PeriodValue.Start == rule.Value.PeriodStart && value.PeriodValue.End == rule.Value.PeriodEnd, rule.Operator),
            AttributeType.Checkbox when value.CheckboxValue.HasValue =>
                CompareEquality(value.CheckboxValue.Value == rule.Value.BooleanValue, rule.Operator),
            AttributeType.Dropdown when value.DropdownOptionId.HasValue =>
                CompareEquality(value.DropdownOptionId.Value == rule.Value.DropdownOptionId, rule.Operator),
            _ => false
        };
    }

    private static bool CompareStrings(string? value, AccessRule rule)
    {
        if (value is null)
            return false;

        var equals = string.Equals(value, rule.Value.StringValue, StringComparison.OrdinalIgnoreCase);

        return CompareEquality(equals, rule.Operator);
    }

    private static bool Compare(int comparison, Operator op) => op switch
    {
        Operator.Equal => comparison == 0,
        Operator.NotEqual => comparison != 0,
        Operator.GreaterThan => comparison > 0,
        Operator.GreaterThanOrEqual => comparison >= 0,
        Operator.LessThan => comparison < 0,
        Operator.LessThanOrEqual => comparison <= 0,
        _ => false
    };

    private static bool CompareEquality(bool equals, Operator op) => op switch
    {
        Operator.Equal => equals,
        Operator.NotEqual => !equals,
        _ => false
    };
}
