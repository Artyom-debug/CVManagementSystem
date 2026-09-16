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

public sealed record CompleteInitialProfileCommand(
    Guid ProfileId,
    IReadOnlyCollection<AttributeValueDto> Values,
    int Version) : IRequest<Result>;

public sealed class CompleteInitialProfileCommandValidator
    : AbstractValidator<CompleteInitialProfileCommand>
{
    public CompleteInitialProfileCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);

        RuleFor(command => command.Values)
            .NotNull()
            .NotEmpty();

        RuleForEach(command => command.Values)
            .SetValidator(new AttributeValueDtoValidator());

        RuleFor(command => command.Values)
            .Must(values => values is null ||
                values.Select(value => value.AttributeId).Distinct().Count() == values.Count)
            .WithMessage("Attribute ids must be unique.");

        RuleFor(command => command.Values)
            .Must(values => values is null ||
                values.Select(value => value.Order).Distinct().Count() == values.Count)
            .WithMessage("Attribute orders must be unique.");
    }
}

internal sealed class CompleteInitialProfileCommandHandler
    : IRequestHandler<CompleteInitialProfileCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public CompleteInitialProfileCommandHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(
        CompleteInitialProfileCommand request,
        CancellationToken cancellationToken)
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

        var systemAttributes = await _context.Attributes
            .AsNoTracking()
            .Include(attribute => attribute.Options)
            .Where(attribute => attribute.IsSystem)
            .ToListAsync(cancellationToken);

        var valuesByAttributeId = request.Values
            .ToDictionary(value => value.AttributeId);

        var systemAttributeIds = systemAttributes
            .Select(attribute => attribute.Id)
            .ToHashSet();

        if (valuesByAttributeId.Keys.Any(id => !systemAttributeIds.Contains(id)))
            return Result.Failure("Only system attributes can be submitted during initial profile setup.");

        var missingAttributes = systemAttributes
            .Where(attribute => !valuesByAttributeId.ContainsKey(attribute.Id))
            .Select(attribute => attribute.Name)
            .ToArray();

        if (missingAttributes.Length > 0)
        {
            return Result.Failure(missingAttributes.Select(name =>
                $"System attribute '{name}' is required."));
        }

        foreach (var attribute in systemAttributes)
        {
            var valueDto = valuesByAttributeId[attribute.Id];
            var value = valueDto.GetValue(attribute.Type);

            var error = ValidateRequiredValue(attribute, value);
            if (error is not null)
                return Result.Failure(error);

            profile.SetAttributeValue(
                attribute.Id,
                value,
                attribute.Type,
                valueDto.Order);
        }

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

    private static string? ValidateRequiredValue(
        Domain.Entities.Attribute attribute,
        object? value)
    {
        if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
            return $"System attribute '{attribute.Name}' is required.";

        if (attribute.Type == AttributeType.Dropdown &&
            value is Guid optionId &&
            attribute.Options.All(option => option.Id != optionId))
        {
            return $"Selected option does not belong to attribute '{attribute.Name}'.";
        }

        return null;
    }
}
