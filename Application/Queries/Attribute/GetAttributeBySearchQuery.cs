using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Attribute;

public sealed record GetAttributeBySearchQuery(
    string Search) : IRequest<IReadOnlyList<AttributeDto>>;

public sealed class GetAttributeBySearchQueryValidator
    : AbstractValidator<GetAttributeBySearchQuery>
{
    public GetAttributeBySearchQueryValidator()
    {
        RuleFor(query => query.Search)
            .NotEmpty()
            .MaximumLength(AttributeValidationConstants.MaximumNameLength);

    }
}

internal sealed class GetAttributeBySearchQueryHandler
    : IRequestHandler<GetAttributeBySearchQuery, IReadOnlyList<AttributeDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAttributeBySearchQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AttributeDto>> Handle(
        GetAttributeBySearchQuery request,
        CancellationToken cancellationToken)
    {
        var search = request.Search.Trim().ToUpperInvariant();

        return await _context.Attributes
            .AsNoTracking()
            .Where(attribute =>
                attribute.Name.StartsWith(search) ||
                EF.Functions.TrigramsAreSimilar(attribute.Name, search))
            .OrderBy(attribute => attribute.Name)
            .Select(attribute => new AttributeDto(
                attribute.Id,
                attribute.Version,
                attribute.Name,
                attribute.Type,
                attribute.Category,
                attribute.IsSystem))
            .ToListAsync(cancellationToken);
    }
}
