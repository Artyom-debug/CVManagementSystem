using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Tag;

public sealed record GetTagBySearchQuery(string Search) : IRequest<IReadOnlyList<string>>;

public sealed class GetTagBySearchQueryValidator : AbstractValidator<GetTagBySearchQuery>
{
    public GetTagBySearchQueryValidator()
    {
        RuleFor(query => query.Search)
            .NotEmpty()
            .MaximumLength(100);
    }
}

internal sealed class GetTagBySearchQueryHandler : IRequestHandler<GetTagBySearchQuery, IReadOnlyList<string>>
{
    private const int ResultLimit = 20;

    private readonly IApplicationDbContext _context;

    public GetTagBySearchQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<string>> Handle(GetTagBySearchQuery request, CancellationToken cancellationToken)
    {
        var search = request.Search.Trim().ToUpperInvariant();

        return await _context.Tags
            .AsNoTracking()
            .Where(tag => tag.Name.StartsWith(search) || EF.Functions.TrigramsAreSimilar(tag.Name, search))
            .OrderBy(tag => tag.Name)
            .Take(ResultLimit)
            .Select(tag => tag.Name)
            .ToListAsync(cancellationToken);
    }
}
