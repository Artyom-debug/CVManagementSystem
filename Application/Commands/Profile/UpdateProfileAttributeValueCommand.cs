using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Profile;

public sealed record UpdateProfileAttributeValueCommand(
    Guid ProfileId,
    AttributeValueDto Value,
    int Version) : IRequest<Result>;

public sealed class UpdateProfileAttributeValueCommandValidator
    : AbstractValidator<UpdateProfileAttributeValueCommand>
{
    public UpdateProfileAttributeValueCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();

        RuleFor(command => command.Value)
            .NotNull()
            .SetValidator(new AttributeValueDtoValidator());

        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateProfileAttributeValueCommandHandler
    : IRequestHandler<UpdateProfileAttributeValueCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public UpdateProfileAttributeValueCommandHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(
        UpdateProfileAttributeValueCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .Include(profile => profile.AttributeValues)
            .SingleOrDefaultAsync(
                profile => profile.Id == request.ProfileId,
                cancellationToken);

        if (profile is null)
            return Result.Failure("Profile was not found.");

        var canManageProfile = profile.UserId == _user.Id ||
                               _user.Roles?.Contains(Roles.Administrator) == true;

        if (!canManageProfile)
            return Result.Failure("You do not have permission to modify this profile.");

        if (profile.Version != request.Version)
            return Result.Failure("The profile was changed by another request. Reload it and try again.");

        var currentValue = profile.AttributeValues
            .SingleOrDefault(value => value.AttributeId == request.Value.AttributeId);

        if (currentValue is null)
            return Result.Failure("Attribute is not added to the profile.");

        var attribute = await _context.Attributes
            .AsNoTracking()
            .Include(attribute => attribute.Options)
            .SingleOrDefaultAsync(
                attribute => attribute.Id == request.Value.AttributeId,
                cancellationToken);

        if (attribute is null)
            return Result.Failure($"Attribute '{request.Value.AttributeId}' was not found.");

        var value = request.Value.GetValue(attribute.Type);

        if (attribute.IsSystem &&
            (value is null || value is string text && string.IsNullOrWhiteSpace(text)))
        {
            return Result.Failure($"System attribute '{attribute.Name}' is required.");
        }

        if (attribute.Type == AttributeType.Dropdown &&
            value is Guid optionId &&
            attribute.Options.All(option => option.Id != optionId))
        {
            return Result.Failure(
                $"Selected option does not belong to attribute '{attribute.Name}'.");
        }

        profile.SetAttributeValue(
            attribute.Id,
            value,
            attribute.Type,
            currentValue.Order);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("The profile was changed by another request. Reload it and try again.");
        }

        return Result.Success(profile.Version);
    }
}
