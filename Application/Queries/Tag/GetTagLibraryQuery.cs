using Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Tag;

public sealed record GetTagLibraryQuery : IRequest<IReadOnlyList<string>>;

internal sealed class GetTagLibraryQueryHandler
    : IRequestHandler<GetTagLibraryQuery, IReadOnlyList<string>>
{
    private readonly IApplicationDbContext _context;

    public GetTagLibraryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<string>> Handle(
        GetTagLibraryQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Tags
            .AsNoTracking()
            .OrderBy(tag => tag.Name)
            .Select(tag => tag.Name)
            .ToListAsync(cancellationToken);
    }
}
