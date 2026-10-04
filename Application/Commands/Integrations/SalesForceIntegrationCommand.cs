using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Integrations;

public sealed record SalesForceIntegrationCommand(Guid ProfileId, SalesForceDto Data) : IRequest<Result>;

public sealed class SalesForceIntegrationCommandValidator : AbstractValidator<SalesForceIntegrationCommand>
{
    public SalesForceIntegrationCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Data).NotNull();

        When(command => command.Data is not null, () =>
        {
            RuleFor(command => command.Data.OrganizationName).MaximumLength(255);
            RuleFor(command => command.Data.FirstName).NotEmpty().MaximumLength(40);
            RuleFor(command => command.Data.LastName).NotEmpty().MaximumLength(80);
            RuleFor(command => command.Data.Email).NotEmpty().EmailAddress().MaximumLength(80);
            RuleFor(command => command.Data.OrganizationPhone).MaximumLength(40);
            RuleFor(command => command.Data.OrganizationWebSite).MaximumLength(255);
            RuleFor(command => command.Data.Industry).MaximumLength(40);
            RuleFor(command => command.Data.Position).MaximumLength(128);
            RuleFor(command => command.Data.Phone).MaximumLength(40);
        });
    }
}

internal sealed class SalesForceIntegrationCommandHandler : IRequestHandler<SalesForceIntegrationCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IUser _user;
    private readonly ISalesForceService _salesForce;

    public SalesForceIntegrationCommandHandler(
        IApplicationDbContext context,
        IIdentityService identityService,
        IUser user,
        ISalesForceService salesForce)
    {
        _context = context;
        _identityService = identityService;
        _user = user;
        _salesForce = salesForce;
    }

    public async Task<Result> Handle(SalesForceIntegrationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var profile = await _context.Profiles
            .AsNoTracking()
            .Where(profile => profile.Id == request.ProfileId)
            .Select(profile => new { profile.UserId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Profile), request.ProfileId);

        if (profile.UserId != _user.Id && _user.Roles?.Contains(Roles.Administrator) != true)
            throw new ForbiddenAccessException("You do not have permission to send this profile to Salesforce.");

        var emails = await _identityService.GetUserEmailsAsync([profile.UserId], cancellationToken);
        if (!emails.TryGetValue(profile.UserId, out var profileEmail) || string.IsNullOrWhiteSpace(profileEmail))
            return Result.Failure("Profile owner email was not found.");

        profileEmail = profileEmail.Trim();
        if (!string.Equals(request.Data.Email.Trim(), profileEmail, StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Email must match the profile owner's email.");

        var data = request.Data with
        {
            OrganizationName = string.IsNullOrWhiteSpace(request.Data.OrganizationName)
                ? $"Individual - {request.Data.FirstName.Trim()} {request.Data.LastName.Trim()}"
                : request.Data.OrganizationName.Trim(),
            OrganizationPhone = NormalizeOptional(request.Data.OrganizationPhone),
            OrganizationWebSite = NormalizeOptional(request.Data.OrganizationWebSite),
            Industry = NormalizeOptional(request.Data.Industry),
            FirstName = request.Data.FirstName.Trim(),
            LastName = request.Data.LastName.Trim(),
            Email = profileEmail,
            Position = NormalizeOptional(request.Data.Position),
            Phone = NormalizeOptional(request.Data.Phone)
        };

        await _salesForce.CreateAccountWithContactAsync(data, cancellationToken);
        return Result.Success();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
