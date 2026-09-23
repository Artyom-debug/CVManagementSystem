using Application.Dtos;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Attribute;

public sealed record GetRecentlyUsedAttributesQuery() : IRequest<IReadOnlyList<AttributeDto>>;

internal sealed class GetRecentlyUsedAttributesQueryHandler : IRequestHandler<GetRecentlyUsedAttributesQuery, IReadOnlyList<AttributeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IRecentAttributesCache _cache;
    private readonly IUser _user;

    public GetRecentlyUsedAttributesQueryHandler(IApplicationDbContext context, IRecentAttributesCache cache, IUser user)
    {
        _context = context;
        _cache = cache;
        _user = user;
    }

    public async Task<IReadOnlyList<AttributeDto>> Handle(GetRecentlyUsedAttributesQuery request, CancellationToken cancellationToken)
    {
        var userId = _user.Id ?? throw new UnauthorizedAccessException("The user is not authenticated.");

        var attributeIds = await _cache.GetAsync(userId, cancellationToken);

        if (attributeIds.Count == 0)
            return Array.Empty<AttributeDto>();

        var attributes = await _context.Attributes
            .AsNoTracking()
            .Where(attribute => attributeIds.Contains(attribute.Id))
            .Select(attribute => new AttributeDto(attribute.Id, attribute.Version, attribute.Name, attribute.Type, attribute.Category, attribute.IsSystem))
            .ToListAsync(cancellationToken);

        var displayOrder = attributeIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);

        return attributes
            .OrderBy(attribute => displayOrder[attribute.Id])
            .ToList();
    }
}
