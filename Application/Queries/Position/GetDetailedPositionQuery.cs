using Application.Common.Exceptions;
using Application.Dtos;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Position;

public sealed record GetDetailedPositionQuery(Guid PositionId)
    : IRequest<DetailedPositionDto>;

public sealed class GetDetailedPositionQueryValidator
    : AbstractValidator<GetDetailedPositionQuery>
{
    public GetDetailedPositionQueryValidator()
    {
        RuleFor(query => query.PositionId).NotEmpty();
    }
}

internal sealed class GetDetailedPositionQueryHandler
    : IRequestHandler<GetDetailedPositionQuery, DetailedPositionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public GetDetailedPositionQueryHandler(
        IApplicationDbContext context,
        ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<DetailedPositionDto> Handle(
        GetDetailedPositionQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"position-details:v1:{request.PositionId}";
        var cachedPosition = await _cache.GetAsync<DetailedPositionDto>(
            cacheKey,
            cancellationToken);

        if (cachedPosition is not null)
            return cachedPosition;

        var position = await _context.Positions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(position => position.Tags)
            .Include(position => position.AccessRules)
            .Include(position => position.DiscussionPosts)
            .Include(position => position.PositionAttributes)
                .ThenInclude(item => item.Attribute)
                    .ThenInclude(attribute => attribute!.Options)
            .SingleOrDefaultAsync(
                position => position.Id == request.PositionId,
                cancellationToken)
            ?? throw new NotFoundException(
                nameof(Domain.Entities.Position),
                request.PositionId);

        var result = new DetailedPositionDto(
            position.Id,
            position.Version,
            position.Name,
            position.Description,
            position.MaxProjectCount,
            position.IsPublic,
            position.Tags
                .OrderBy(tag => tag.Name)
                .Select(tag => tag.Name)
                .ToList(),
            position.PositionAttributes
                .OrderBy(item => item.DisplayOrder)
                .Select(item => new PositionAttributeDto(
                    item.DisplayOrder,
                    new DetailedAttributeDto(
                        item.Attribute!.Id,
                        item.Attribute.Version,
                        item.Attribute.Name,
                        item.Attribute.Description,
                        item.Attribute.Type,
                        item.Attribute.Category,
                        item.Attribute.IsSystem,
                        item.Attribute.Options
                            .OrderBy(option => option.Option)
                            .Select(option => new AttributeOptionDto(
                                option.Id,
                                option.Option))
                            .ToList())))
                .ToList(),
            position.AccessRules
                .Select(rule => new PositionAccessRuleDto(
                    rule.AttributeId,
                    rule.AttributeType,
                    rule.Operator,
                    rule.Value.StringValue,
                    rule.Value.NumericValue,
                    rule.Value.DateValue,
                    rule.Value.PeriodStart,
                    rule.Value.PeriodEnd,
                    rule.Value.BooleanValue,
                    rule.Value.DropdownOptionId))
                .ToList(),
            position.DiscussionPosts
                .OrderBy(post => post.CreatedAt)
                .Select(post => new DiscussionPostDto(
                    post.Id,
                    post.AuthorId,
                    post.Content,
                    post.CreatedAt))
                .ToList());

        var dependencies = position.PositionAttributes
            .Select(item => $"attribute:{item.AttributeId}")
            .Append($"position:{position.Id}")
            .Distinct()
            .ToArray();

        await _cache.SetAsync(
            cacheKey,
            result,
            TimeSpan.FromMinutes(10),
            cancellationToken,
            dependencies);

        return result;
    }
}
