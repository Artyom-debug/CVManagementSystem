using Application.Common.Exceptions;
using Application.Dtos;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Attribute;

public sealed record GetDetailedAttributeQuery(
    Guid AttributeId) : IRequest<DetailedAttributeDto>;

public sealed class GetDetailedAttributeQueryValidator
    : AbstractValidator<GetDetailedAttributeQuery>
{
    public GetDetailedAttributeQueryValidator()
    {
        RuleFor(query => query.AttributeId).NotEmpty();
    }
}

internal sealed class GetDetailedAttributeQueryHandler
    : IRequestHandler<GetDetailedAttributeQuery, DetailedAttributeDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public GetDetailedAttributeQueryHandler(
        IApplicationDbContext context,
        ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<DetailedAttributeDto> Handle(
        GetDetailedAttributeQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"attribute-details:v1:{request.AttributeId}";

        var cachedAttribute = await _cache.GetAsync<DetailedAttributeDto>(
            cacheKey,
            cancellationToken);

        if (cachedAttribute is not null)
            return cachedAttribute;

        var attribute = await _context.Attributes
            .AsNoTracking()
            .Where(attribute => attribute.Id == request.AttributeId)
            .Select(attribute => new DetailedAttributeDto(
                attribute.Id,
                attribute.Version,
                attribute.Name,
                attribute.Description,
                attribute.Type,
                attribute.Category,
                attribute.IsSystem,
                attribute.Options
                    .OrderBy(option => option.Option)
                    .Select(option => new AttributeOptionDto(option.Id, option.Option))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);

        if (attribute is null)
            throw new NotFoundException(nameof(Domain.Entities.Attribute), request.AttributeId);

        await _cache.SetAsync(
            cacheKey,
            attribute,
            TimeSpan.FromMinutes(15),
            cancellationToken,
            [$"attribute:{request.AttributeId}"]);

        return attribute;
    }
}
