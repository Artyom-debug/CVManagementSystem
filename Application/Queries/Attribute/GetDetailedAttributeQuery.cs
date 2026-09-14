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

    public GetDetailedAttributeQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DetailedAttributeDto> Handle(
        GetDetailedAttributeQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Attributes
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
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Attribute), request.AttributeId);
    }
}
