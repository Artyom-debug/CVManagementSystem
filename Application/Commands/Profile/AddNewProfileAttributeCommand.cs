using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Profile;

public sealed record AddNewProfileAttributeCommand(Guid ProfileId, AttributeValueDto Value, int Version) : IRequest<Result>;

public sealed class AddNewProfileAttributeCommandValidator : AbstractValidator<AddNewProfileAttributeCommand>
{
    public AddNewProfileAttributeCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();

        RuleFor(command => command.Value)
            .NotNull()
            .SetValidator(new AttributeValueDtoValidator());

        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}

internal sealed class AddNewProfileAttributeCommandHandler : IRequestHandler<AddNewProfileAttributeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IRecentAttributesCache _recentAttributesCache;

    public AddNewProfileAttributeCommandHandler(IApplicationDbContext context, IUser user, IRecentAttributesCache recentAttributesCache)
    {
        _context = context;
        _user = user;
        _recentAttributesCache = recentAttributesCache;

    }

    public async Task<Result> Handle(AddNewProfileAttributeCommand request, CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .Include(item => item.AttributeValues)
            .SingleOrDefaultAsync(item => item.Id == request.ProfileId, cancellationToken);

        if (profile is null)
            return Result.Failure("Profile was not found.");

        var canManageProfile = profile.UserId == _user.Id ||
                               _user.Roles?.Contains(Roles.Administrator) == true;

        if (!canManageProfile)
            return Result.Failure("You do not have permission to modify this profile.");

        var attribute = await _context.Attributes
            .AsNoTracking()
            .Include(item => item.Options)
            .SingleOrDefaultAsync(item => item.Id == request.Value.AttributeId, cancellationToken);

        if (attribute is null)
            return Result.Failure($"Attribute '{request.Value.AttributeId}' was not found.");

        if (attribute.IsSystem)
            return Result.Failure("System attributes are added during initial profile setup.");

        if (profile.AttributeValues.Any(value => value.AttributeId == attribute.Id))
            return Result.Failure($"Attribute '{attribute.Name}' is already added to the profile.");

        var value = request.Value.GetValue(attribute.Type);

        if (attribute.Type == AttributeType.Dropdown &&
            value is Guid optionId &&
            attribute.Options.All(option => option.Id != optionId))
        {
            return Result.Failure($"Selected option does not belong to attribute '{attribute.Name}'.");
        }

        profile.SetAttributeValue(attribute.Id, value, attribute.Type, request.Value.Order);

        profile.AddDomainEvent(new ProfileChangedEvent(profile.Id));
        _context.SetOriginalVersion(profile, request.Version);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("The profile was changed by another request. Reload it and try again.");
        }
        await _recentAttributesCache.AddAsync(_user.Id!, attribute.Id, cancellationToken);
        return Result.Success(profile.Version);
    }
}

internal sealed class AttributeValueDtoValidator : AbstractValidator<AttributeValueDto>
{
    public AttributeValueDtoValidator()
    {
        RuleFor(value => value.AttributeId).NotEmpty();
        RuleFor(value => value.Order).GreaterThanOrEqualTo(0);
    }
}
