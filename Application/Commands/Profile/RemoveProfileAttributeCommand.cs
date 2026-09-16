using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Profile;

public sealed record RemoveProfileAttributeCommand(
    Guid ProfileId,
    Guid AttributeId,
    int Version) : IRequest<Result>;

public sealed class RemoveProfileAttributeCommandValidator
    : AbstractValidator<RemoveProfileAttributeCommand>
{
    public RemoveProfileAttributeCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.AttributeId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}

internal sealed class RemoveProfileAttributeCommandHandler
    : IRequestHandler<RemoveProfileAttributeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public RemoveProfileAttributeCommandHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(
        RemoveProfileAttributeCommand request,
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

        var attribute = await _context.Attributes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                attribute => attribute.Id == request.AttributeId,
                cancellationToken);

        if (attribute is null)
            return Result.Failure($"Attribute '{request.AttributeId}' was not found.");

        if (attribute.IsSystem)
            return Result.Failure("System attributes cannot be removed from a profile.");

        if (profile.AttributeValues.All(value => value.AttributeId != attribute.Id))
            return Result.Failure($"Attribute '{attribute.Name}' is not added to the profile.");

        profile.RemoveAttributeValue(attribute);

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

        return Result.Success(profile.Version);
    }
}
