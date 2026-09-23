using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Dtos;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Position;

public sealed record GetAvailablePositionsQuery(int Page = 1, int PageSize = 30) : IRequest<PositionsPageDto>;

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

internal sealed class GetAvailablePositionsQueryHandler : IRequestHandler<GetAvailablePositionsQuery, PositionsPageDto>
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

    public async Task<PositionsPageDto> Handle(GetAvailablePositionsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var cacheKey = $"positions:available:v6:user:{_user.Id}:page:{request.Page}:size:{request.PageSize}";
        var cachedResult = await _cache.GetAsync<PositionsPageDto>(cacheKey, cancellationToken);

        if (cachedResult is not null)
            return cachedResult;

        var profileId = await _context.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == _user.Id)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Profile), _user.Id);

        var skip = (request.Page - 1) * request.PageSize;

        var accessiblePositions = _context.Positions
            .AsNoTracking()
            .Where(position =>
                position.IsPublic ||
                position.AccessRules.Any() &&
                !position.AccessRules.Any(rule =>
                    !_context.ProfileAttributes.Any(value =>
                        value.ProfileId == profileId &&
                        value.AttributeId == rule.AttributeId &&
                        (
                            rule.AttributeType == AttributeType.String &&
                            value.StringValue != null &&
                            rule.Value.StringValue != null &&
                            (rule.Operator == Operator.Equal && value.StringValue.ToUpper() == rule.Value.StringValue.ToUpper() ||
                             rule.Operator == Operator.NotEqual && value.StringValue.ToUpper() != rule.Value.StringValue.ToUpper()) ||

                            rule.AttributeType == AttributeType.Text &&
                            value.TextValue != null &&
                            rule.Value.StringValue != null &&
                            (rule.Operator == Operator.Equal && value.TextValue.ToUpper() == rule.Value.StringValue.ToUpper() ||
                             rule.Operator == Operator.NotEqual && value.TextValue.ToUpper() != rule.Value.StringValue.ToUpper()) ||

                            rule.AttributeType == AttributeType.Image &&
                            value.ImageValue != null &&
                            rule.Value.StringValue != null &&
                            (rule.Operator == Operator.Equal && value.ImageValue.ToUpper() == rule.Value.StringValue.ToUpper() ||
                             rule.Operator == Operator.NotEqual && value.ImageValue.ToUpper() != rule.Value.StringValue.ToUpper()) ||

                            rule.AttributeType == AttributeType.Numeric &&
                            value.NumericValue.HasValue &&
                            rule.Value.NumericValue.HasValue &&
                            (rule.Operator == Operator.Equal && value.NumericValue == rule.Value.NumericValue ||
                             rule.Operator == Operator.NotEqual && value.NumericValue != rule.Value.NumericValue ||
                             rule.Operator == Operator.GreaterThan && value.NumericValue > rule.Value.NumericValue ||
                             rule.Operator == Operator.GreaterThanOrEqual && value.NumericValue >= rule.Value.NumericValue ||
                             rule.Operator == Operator.LessThan && value.NumericValue < rule.Value.NumericValue ||
                             rule.Operator == Operator.LessThanOrEqual && value.NumericValue <= rule.Value.NumericValue) ||

                            rule.AttributeType == AttributeType.Date &&
                            value.DateValue.HasValue &&
                            rule.Value.DateValue.HasValue &&
                            (rule.Operator == Operator.Equal && value.DateValue == rule.Value.DateValue ||
                             rule.Operator == Operator.NotEqual && value.DateValue != rule.Value.DateValue ||
                             rule.Operator == Operator.GreaterThan && value.DateValue > rule.Value.DateValue ||
                             rule.Operator == Operator.GreaterThanOrEqual && value.DateValue >= rule.Value.DateValue ||
                             rule.Operator == Operator.LessThan && value.DateValue < rule.Value.DateValue ||
                             rule.Operator == Operator.LessThanOrEqual && value.DateValue <= rule.Value.DateValue) ||

                            rule.AttributeType == AttributeType.Period &&
                            value.PeriodValue != null &&
                            rule.Value.PeriodStart.HasValue &&
                            (rule.Operator == Operator.Equal &&
                             value.PeriodValue.Start == rule.Value.PeriodStart &&
                             value.PeriodValue.End == rule.Value.PeriodEnd ||
                             rule.Operator == Operator.NotEqual &&
                             (value.PeriodValue.Start != rule.Value.PeriodStart ||
                              value.PeriodValue.End != rule.Value.PeriodEnd)) ||

                            rule.AttributeType == AttributeType.Checkbox &&
                            value.CheckboxValue.HasValue &&
                            rule.Value.BooleanValue.HasValue &&
                            (rule.Operator == Operator.Equal && value.CheckboxValue == rule.Value.BooleanValue ||
                             rule.Operator == Operator.NotEqual && value.CheckboxValue != rule.Value.BooleanValue) ||

                            rule.AttributeType == AttributeType.Dropdown &&
                            value.DropdownOptionId.HasValue &&
                            rule.Value.DropdownOptionId.HasValue &&
                            (rule.Operator == Operator.Equal && value.DropdownOptionId == rule.Value.DropdownOptionId ||
                             rule.Operator == Operator.NotEqual && value.DropdownOptionId != rule.Value.DropdownOptionId)
                        ))));

        var publishedAfter = DateTime.UtcNow.AddHours(-24);
        var items = await accessiblePositions
            .OrderByDescending(position => position.CreatedAt)
            .Skip(skip)
            .Take(request.PageSize + 1)
            .Select(position => new PositionDto(
                position.Id,
                position.Version,
                position.CreatedAt,
                position.Name,
                position.Description,
                position.MaxProjectCount,
                position.IsPublic,
                position.Tags
                    .OrderBy(tag => tag.Name)
                    .Select(tag => tag.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);

        var hasNextPage = items.Count > request.PageSize;
        if (hasNextPage)
            items.RemoveAt(items.Count - 1);

        var totalAvailablePositions = items.Count;

        var cvStatistics = await _context.CVs
            .AsNoTracking()
            .Where(cv => cv.Status == Status.Published)
            .GroupBy(cv => 1)
            .Select(group => new
            {
                TotalSubmittedCVs = group.Count(),
                PublishedCVsLast24Hours = group.Count(cv => cv.PublishedAt >= publishedAfter)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var positions = new PageResult<PositionDto>(items, request.Page, request.PageSize, hasNextPage);
        var result = new PositionsPageDto(positions, totalAvailablePositions, cvStatistics?.TotalSubmittedCVs ?? 0, cvStatistics?.PublishedCVsLast24Hours ?? 0);

        await _cache.SetAsync(cacheKey, result, request.Page <= 3 ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(2), cancellationToken, ["position-library", $"profile:{profileId}"]);

        return result;
    }
}
