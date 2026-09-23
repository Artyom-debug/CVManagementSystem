using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Application.Common.Exceptions;

namespace Application.Commands.Profile;

public sealed record UpdateProfileAttributeValueCommand(Guid ProfileId, AttributeValueDto Value, int Version) : IRequest<Result>;

public sealed class UpdateProfileAttributeValueCommandValidator : AbstractValidator<UpdateProfileAttributeValueCommand>
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

internal sealed class UpdateProfileAttributeValueCommandHandler : IRequestHandler<UpdateProfileAttributeValueCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly IImageStorage _imageStorage;

    public UpdateProfileAttributeValueCommandHandler(IApplicationDbContext context, IUser user, IImageStorage imageStorage)
    {
        _context = context;
        _user = user;
        _imageStorage = imageStorage;
    }

    public async Task<Result> Handle(UpdateProfileAttributeValueCommand request, CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .Include(profile => profile.AttributeValues)
            .SingleOrDefaultAsync(profile => profile.Id == request.ProfileId, cancellationToken);

        if (profile is null)
            return Result.Failure("Profile was not found.");

        var canManageProfile = profile.UserId == _user.Id ||
                               _user.Roles?.Contains(Roles.Administrator) == true;

        if (!canManageProfile)
            throw new ForbiddenAccessException("You do not have permission to manage this profile");

        var currentValue = profile.AttributeValues
            .SingleOrDefault(value => value.AttributeId == request.Value.AttributeId);

        if (currentValue is null)
            return Result.Failure("Attribute is not added to the profile.");

        var attribute = await _context.Attributes
            .AsNoTracking()
            .Include(attribute => attribute.Options)
            .SingleOrDefaultAsync(attribute => attribute.Id == request.Value.AttributeId, cancellationToken);

        if (attribute is null)
            return Result.Failure($"Attribute '{request.Value.AttributeId}' was not found.");

        var isRecruiterOnly = _user.Roles?.Contains(Roles.Recruiter) == true &&
                              _user.Roles.Contains(Roles.Candidate) == false &&
                              _user.Roles.Contains(Roles.Administrator) == false;

        if (isRecruiterOnly && !attribute.IsSystem)
            throw new ForbiddenAccessException("Recruiters can update only system profile attributes.");

        var value = request.Value.GetValue(attribute.Type);

        if (attribute.IsSystem && (value is null || value is string text && string.IsNullOrWhiteSpace(text)))
            return Result.Failure($"System attribute '{attribute.Name}' is required.");

        if (attribute.Type == AttributeType.Dropdown && value is Guid optionId && attribute.Options.All(option => option.Id != optionId))
            return Result.Failure($"Selected option does not belong to attribute '{attribute.Name}'.");

        if (attribute.Type == AttributeType.Image &&
            value is string publicId &&
            !string.IsNullOrWhiteSpace(publicId))
        {
            var expectedPrefix = $"profiles/{profile.Id}/attributes/{attribute.Id}/";
            var imageExists = publicId.StartsWith(expectedPrefix, StringComparison.Ordinal) &&
                              await _imageStorage.ExistsAsync(publicId, cancellationToken);

            if (!imageExists)
                return Result.Failure("The uploaded image was not found.");
        }

        profile.SetAttributeValue(attribute.Id, value, attribute.Type, currentValue.Order);

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
