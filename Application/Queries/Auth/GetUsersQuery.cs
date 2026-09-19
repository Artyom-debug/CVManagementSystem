using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Queries.Auth;

public sealed record GetUsersQuery(int Page = 1, int PageSize = 30, string? Search = null, string? Role = null, bool? IsBlocked = null) : IRequest<PageResult<IdentityUserDto>>;

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Search).MaximumLength(256);
        RuleFor(query => query.Role)
            .Must(role => role is null or Roles.Candidate or Roles.Recruiter or Roles.Administrator)
            .WithMessage("Unknown user role.");
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithName(nameof(GetUsersQuery.Page))
            .WithMessage("Requested page is outside the supported range.");
    }
}

internal sealed class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PageResult<IdentityUserDto>>
{
    private readonly IIdentityService _identityService;

    public GetUsersQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<PageResult<IdentityUserDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken) =>
        _identityService.GetUsersAsync(request.Page, request.PageSize, request.Search, request.Role, request.IsBlocked, cancellationToken);
}
