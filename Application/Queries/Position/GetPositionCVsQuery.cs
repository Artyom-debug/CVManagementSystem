using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Position;

public sealed record GetPositionCVsQuery(Guid PositionId, int Page = 1, int PageSize = 30) : IRequest<PageResult<PositionCVDto>>;

public sealed class GetPositionCVsQueryValidator : AbstractValidator<GetPositionCVsQuery>
{
    public GetPositionCVsQueryValidator()
    {
        RuleFor(query => query.PositionId).NotEmpty();
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithName(nameof(GetPositionCVsQuery.Page))
            .WithMessage("Requested page is outside the supported range.");
    }
}

internal sealed class GetPositionCVsQueryHandler : IRequestHandler<GetPositionCVsQuery, PageResult<PositionCVDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IIdentityService _identityService;

    public GetPositionCVsQueryHandler(IApplicationDbContext context, IUser user, IIdentityService identityService)
    {
        _context = context;
        _user = user;
        _identityService = identityService;
    }

    public async Task<PageResult<PositionCVDto>> Handle(GetPositionCVsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var isAdministrator = _user.Roles?.Contains(Roles.Administrator) == true;
        var isRecruiter = _user.Roles?.Contains(Roles.Recruiter) == true;

        if (!isAdministrator && !isRecruiter)
            throw new ForbiddenAccessException("Only recruiters and administrators can view candidate CVs.");

        var positionExists = await _context.Positions
            .AsNoTracking()
            .AnyAsync(position => position.Id == request.PositionId, cancellationToken);

        if (!positionExists)
            throw new NotFoundException(nameof(Domain.Entities.Position), request.PositionId);

        var skip = (request.Page - 1) * request.PageSize;
        var rows = await _context.CVs
            .AsNoTracking()
            .Where(cv =>
                cv.PositionId == request.PositionId &&
                cv.Status == Status.Published)
            .OrderByDescending(cv => cv.CreatedAt)
            .Skip(skip)
            .Take(request.PageSize + 1)
            .Select(cv => new
            {
                cv.Id,
                cv.ProfileId,
                cv.Profile!.UserId,
                cv.Status,
                cv.CreatedAt,
                cv.PublishedAt,
                LikesCount = cv.Likes.Count
            })
            .ToListAsync(cancellationToken);

        var hasNextPage = rows.Count > request.PageSize;
        if (hasNextPage)
            rows.RemoveAt(rows.Count - 1);

        var userIds = rows
            .Select(row => row.UserId)
            .Distinct()
            .ToArray();

        var emails = await _identityService.GetUserEmailsAsync(userIds, cancellationToken);
        var items = rows
            .Select(row => new PositionCVDto(
                row.Id,
                row.ProfileId,
                emails.GetValueOrDefault(row.UserId),
                row.Status,
                row.CreatedAt,
                row.PublishedAt,
                row.LikesCount))
            .ToList();

        return new PageResult<PositionCVDto>(items, request.Page, request.PageSize, hasNextPage);
    }
}
