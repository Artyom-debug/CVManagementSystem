using Application.Common.Models;
using Application.Dtos;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Position;

public sealed record GetRecruiterPositionsQuery(
    int Page = 1,
    int PageSize = 30) : IRequest<PageResult<PositionDto>>;

public sealed class GetRecruiterPositionsQueryValidator
    : AbstractValidator<GetRecruiterPositionsQuery>
{
    public GetRecruiterPositionsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithName(nameof(GetRecruiterPositionsQuery.Page))
            .WithMessage("Requested page is outside the supported range.");
    }
}

internal sealed class GetRecruiterPositionsQueryHandler
    : IRequestHandler<GetRecruiterPositionsQuery, PageResult<PositionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public GetRecruiterPositionsQueryHandler(
        IApplicationDbContext context,
        ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PageResult<PositionDto>> Handle(
        GetRecruiterPositionsQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"positions:recruiter:v1:page:{request.Page}:size:{request.PageSize}";
        var cachedResult = await _cache.GetAsync<PageResult<PositionDto>>(
            cacheKey,
            cancellationToken);

        if (cachedResult is not null)
            return cachedResult;

        var skip = (request.Page - 1) * request.PageSize;
        var items = await _context.Positions
            .AsNoTracking()
            .OrderBy(position => position.Name)
            .ThenBy(position => position.Id)
            .Skip(skip)
            .Take(request.PageSize + 1)
            .Select(position => new PositionDto(
                position.Id,
                position.Version,
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

        var result = new PageResult<PositionDto>(
            items,
            request.Page,
            request.PageSize,
            hasNextPage);

        await _cache.SetAsync(
            cacheKey,
            result,
            request.Page <= 3 ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(2),
            cancellationToken,
            ["position-library"]);

        return result;
    }
}
