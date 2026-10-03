using Application.Common.Exceptions;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Integrations;

public sealed record GetProfileAttributesForSalesForceQuery(Guid ProfileId) : IRequest<SalesForcePrefillDto>;

public sealed class GetProfileAttributesForSalesForceQueryValidator : AbstractValidator<GetProfileAttributesForSalesForceQuery>
{
    public GetProfileAttributesForSalesForceQueryValidator()
    {
        RuleFor(query => query.ProfileId).NotEmpty();
    }
}

internal sealed class GetProfileAttributesForSalesForceQueryHandler : IRequestHandler<GetProfileAttributesForSalesForceQuery, SalesForcePrefillDto>
{
    private static readonly string[] FirstNameNames = ["FIRST NAME", "GIVEN NAME"];
    private static readonly string[] LastNameNames = ["LAST NAME", "SURNAME"];
    private static readonly string[] OrganizationNameNames = ["ORGANIZATION NAME", "COMPANY NAME", "COMPANY", "ORGANIZATION", "EMPLOYER"];
    private static readonly string[] OrganizationPhoneNames = ["ORGANIZATION PHONE", "COMPANY PHONE", "WORK PHONE"];
    private static readonly string[] OrganizationWebSiteNames = ["ORGANIZATION WEBSITE", "COMPANY WEBSITE", "ORGANIZATION WEB SITE", "COMPANY WEB SITE", "WEBSITE"];
    private static readonly string[] IndustryNames = ["INDUSTRY", "BUSINESS INDUSTRY"];
    private static readonly string[] PositionNames = ["JOB TITLE", "CURRENT JOB TITLE", "POSITION", "CURRENT POSITION", "TITLE"];
    private static readonly string[] PhoneNames = ["PHONE NUMBER", "PERSONAL PHONE", "MOBILE PHONE", "PHONE"];
    private static readonly string[] AttributeNames = FirstNameNames
        .Concat(LastNameNames)
        .Concat(OrganizationNameNames)
        .Concat(OrganizationPhoneNames)
        .Concat(OrganizationWebSiteNames)
        .Concat(IndustryNames)
        .Concat(PositionNames)
        .Concat(PhoneNames)
        .Distinct()
        .ToArray();

    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IUser _user;

    public GetProfileAttributesForSalesForceQueryHandler(IApplicationDbContext context, IIdentityService identityService, IUser user)
    {
        _context = context;
        _identityService = identityService;
        _user = user;
    }

    public async Task<SalesForcePrefillDto> Handle(GetProfileAttributesForSalesForceQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var profile = await _context.Profiles
            .AsNoTracking()
            .Where(profile => profile.Id == request.ProfileId)
            .Select(profile => new { profile.Id, profile.UserId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Profile), request.ProfileId);

        if (profile.UserId != _user.Id && _user.Roles?.Contains(Roles.Administrator) != true)
            throw new ForbiddenAccessException("You do not have permission to access this profile's Salesforce form.");

        var attributes = await _context.ProfileAttributes
            .AsNoTracking()
            .Where(value => value.ProfileId == profile.Id && AttributeNames.Contains(value.Attribute!.Name))
            .Select(value => new
            {
                value.Attribute!.Name,
                value.Attribute.Type,
                value.StringValue,
                value.TextValue,
                DropdownValue = value.DropdownOption == null ? null : value.DropdownOption.Option
            })
            .ToListAsync(cancellationToken);

        var values = attributes
            .Select(attribute => new ProfileValue(attribute.Name, attribute.Type switch
            {
                AttributeType.String => attribute.StringValue,
                AttributeType.Text => attribute.TextValue,
                AttributeType.Dropdown => attribute.DropdownValue,
                _ => null
            }))
            .ToArray();

        var emails = await _identityService.GetUserEmailsAsync([profile.UserId], cancellationToken);
        emails.TryGetValue(profile.UserId, out var email);

        return new SalesForcePrefillDto(
            GetValue(values, FirstNameNames),
            GetValue(values, LastNameNames),
            email,
            GetValue(values, OrganizationNameNames),
            GetValue(values, OrganizationPhoneNames),
            GetValue(values, OrganizationWebSiteNames),
            GetValue(values, IndustryNames),
            GetValue(values, PositionNames),
            GetValue(values, PhoneNames));
    }

    private static string? GetValue(IReadOnlyCollection<ProfileValue> values, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            var value = values.FirstOrDefault(value => value.Name == name && !string.IsNullOrWhiteSpace(value.Value))?.Value;
            if (value is not null)
                return value.Trim();
        }

        return null;
    }

    private sealed record ProfileValue(string Name, string? Value);
}
