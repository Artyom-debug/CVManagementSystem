using Application.Common.Models;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Attribute;

public sealed record GetAttributesQuery(
    int Page = 1,
    int PageSize = 30,
    Category? Category = null) : IRequest<PageResult<AttributeDto>>;

public sealed class GetAttributesQueryValidator
    : AbstractValidator<GetAttributesQuery>
{
    public GetAttributesQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Category)
            .Must(category => category is null || Enum.IsDefined(category.Value))
            .WithMessage("Unknown attribute category.");
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithName(nameof(GetAttributesQuery.Page))
            .WithMessage("Requested page is outside the supported range.");
    }
}

internal sealed class GetAttributesQueryHandler : IRequestHandler<GetAttributesQuery, PageResult<AttributeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAttributesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PageResult<AttributeDto>> Handle(GetAttributesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Attributes.AsNoTracking();

        if (request.Category.HasValue)
            query = query.Where(attribute => attribute.Category == request.Category.Value);

        var skip = (request.Page - 1) * request.PageSize;
        var items = await query
            .OrderBy(attribute => attribute.Name)
            .ThenBy(attribute => attribute.Id)
            .Skip(skip)
            .Take(request.PageSize + 1)
            .Select(attribute => new AttributeDto(
                attribute.Id,
                attribute.Version,
                attribute.Name,
                attribute.Type,
                attribute.Category,
                attribute.IsSystem))
            .ToListAsync(cancellationToken);

        var hasNextPage = items.Count > request.PageSize;
        if (hasNextPage)
            items.RemoveAt(items.Count - 1);

        return new PageResult<AttributeDto>(
            items,
            request.Page,
            request.PageSize,
            hasNextPage);
    }
}
