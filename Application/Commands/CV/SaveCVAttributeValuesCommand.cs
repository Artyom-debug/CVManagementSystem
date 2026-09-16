using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.CV;

public sealed record SaveCVAttributeValuesCommand(
    Guid CVId,
    int ProfileVersion,
    IReadOnlyCollection<AttributeValueDto> Values) : IRequest<Result>;

public sealed class SaveCVAttributeValuesCommandValidator
    : AbstractValidator<SaveCVAttributeValuesCommand>
{
    public SaveCVAttributeValuesCommandValidator()
    {
        RuleFor(command => command.CVId).NotEmpty();
        RuleFor(command => command.ProfileVersion).GreaterThanOrEqualTo(0);

        RuleFor(command => command.Values)
            .NotEmpty()
            .Must(values => values is null ||
                values.Select(value => value.AttributeId).Distinct().Count() == values.Count)
            .WithMessage("Attribute ids must be unique.");

        RuleForEach(command => command.Values)
            .ChildRules(value =>
            {
                value.RuleFor(item => item.AttributeId).NotEmpty();
            });
    }
}

internal sealed class SaveCVAttributeValuesCommandHandler
    : IRequestHandler<SaveCVAttributeValuesCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public SaveCVAttributeValuesCommandHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(
        SaveCVAttributeValuesCommand request,
        CancellationToken cancellationToken)
    {
        var cv = await _context.CVs
            .Include(cv => cv.Profile)
                .ThenInclude(profile => profile!.AttributeValues)
            .SingleOrDefaultAsync(
                cv => cv.Id == request.CVId,
                cancellationToken);

        if (cv is null)
            return Result.Failure("CV was not found.");

        var profile = cv.Profile!;
        var canManageCV = profile.UserId == _user.Id ||
                          _user.Roles?.Contains(Roles.Administrator) == true;

        if (!canManageCV)
            return Result.Failure("You do not have permission to modify this CV.");

        if (cv.Status == Status.Deleted)
            return Result.Failure("A deleted CV cannot be modified.");

        var attributeIds = request.Values
            .Select(value => value.AttributeId)
            .ToArray();

        var positionAttributeIds = await _context.PositionAttributes
            .AsNoTracking()
            .Where(positionAttribute =>
                positionAttribute.PositionId == cv.PositionId &&
                attributeIds.Contains(positionAttribute.AttributeId))
            .Select(positionAttribute => positionAttribute.AttributeId)
            .ToHashSetAsync(cancellationToken);

        if (positionAttributeIds.Count != attributeIds.Length)
            return Result.Failure("One or more attributes do not belong to this CV template.");

        var attributes = await _context.Attributes
            .AsNoTracking()
            .Include(attribute => attribute.Options)
            .Where(attribute => attributeIds.Contains(attribute.Id))
            .ToListAsync(cancellationToken);

        if (attributes.Count != attributeIds.Length)
            return Result.Failure("One or more attributes were not found.");

        var attributesById = attributes.ToDictionary(attribute => attribute.Id);
        var valuesToSave = new List<(Domain.Entities.Attribute Attribute, object? Value)>();

        foreach (var item in request.Values)
        {
            var attribute = attributesById[item.AttributeId];
            var value = item.GetValue(attribute.Type);

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

            valuesToSave.Add((attribute, value));
        }

        var currentValues = profile.AttributeValues
            .ToDictionary(value => value.AttributeId);

        var nextOrder = currentValues.Count == 0
            ? 0
            : currentValues.Values.Max(value => value.Order) + 1;

        foreach (var item in valuesToSave)
        {
            var order = currentValues.TryGetValue(item.Attribute.Id, out var currentValue)
                ? currentValue.Order
                : nextOrder++;

            profile.SetAttributeValue(
                item.Attribute.Id,
                item.Value,
                item.Attribute.Type,
                order);
        }

        profile.AddDomainEvent(new ProfileChangedEvent(profile.Id));
        _context.SetOriginalVersion(profile, request.ProfileVersion);

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
