using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Attribute;

public sealed record UpdateAttributeCommand(
    Guid AttributeId,
    int Version,
    string Name,
    string? Description,
    Category Category,
    IReadOnlyCollection<AttributeOptionInput>? Options) : IRequest<Result>;

public sealed class UpdateAttributeCommandValidator
    : AbstractValidator<UpdateAttributeCommand>
{
    public UpdateAttributeCommandValidator()
    {
        RuleFor(command => command.AttributeId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(AttributeValidationConstants.MaximumNameLength);

        RuleFor(command => command.Description)
            .MaximumLength(AttributeValidationConstants.MaximumDescriptionLength);

        RuleFor(command => command.Category).IsInEnum();

        RuleFor(command => command.Options)
            .Must(HaveUniqueIds)
            .WithMessage("Option ids must be unique.")
            .Must(HaveUniqueValues)
            .WithMessage("Option values must be unique.");

        RuleForEach(command => command.Options)
            .NotNull()
            .SetValidator(new AttributeOptionInputValidator());
    }

    private static bool HaveUniqueIds(IReadOnlyCollection<AttributeOptionInput>? options) =>
        options is null || options
            .Where(option => option.Id.HasValue)
            .Select(option => option.Id!.Value)
            .Distinct()
            .Count() == options.Count(option => option.Id.HasValue);

    private static bool HaveUniqueValues(IReadOnlyCollection<AttributeOptionInput>? options) =>
        options is null || options
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .Select(option => option.Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == options.Count(option => !string.IsNullOrWhiteSpace(option.Value));
}

internal sealed class AttributeOptionInputValidator : AbstractValidator<AttributeOptionInput>
{
    public AttributeOptionInputValidator()
    {
        RuleFor(option => option.Id)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Option id cannot be an empty GUID.");

        RuleFor(option => option.Value)
            .NotEmpty()
            .MaximumLength(AttributeValidationConstants.MaximumOptionLength);
    }
}

internal sealed class UpdateAttributeCommandHandler
    : IRequestHandler<UpdateAttributeCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdateAttributeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        UpdateAttributeCommand request,
        CancellationToken cancellationToken)
    {
        var attribute = await _context.Attributes
            .Include(item => item.Options)
            .SingleOrDefaultAsync(item => item.Id == request.AttributeId, cancellationToken);

        if (attribute is null)
            return Result.Failure($"Attribute '{request.AttributeId}' was not found.");

        if (attribute.IsSystem)
            return Result.Failure("System attributes are immutable and cannot be updated.");

        var requestedOptions = request.Options ?? [];
        var optionsError = ValidateOptionsForType(attribute.Type, requestedOptions)
            ?? ValidateRequestedOptionIds(attribute, requestedOptions);

        if (optionsError is not null)
            return Result.Failure(optionsError);

        var normalizedName = request.Name.Trim().ToUpperInvariant();
        if (attribute.Name != normalizedName)
        {
            var nameAlreadyExists = await _context.Attributes
                .AnyAsync(
                    item => item.Id != attribute.Id && item.Name == normalizedName,
                    cancellationToken);

            if (nameAlreadyExists)
                return Result.Failure($"Attribute '{request.Name.Trim()}' already exists.");
        }

        var oldName = attribute.Name;
        var oldDescription = attribute.Description;
        var oldCategory = attribute.Category;
        var oldOptions = attribute.Options.ToDictionary(option => option.Id, option => option.Option);

        attribute.RenameAttribute(request.Name);
        attribute.AddDescription(request.Description ?? string.Empty);
        attribute.ChangeAttributeCategory(request.Category);

        if (attribute.Type == AttributeType.Dropdown)
            SynchronizeDropdownOptions(attribute, requestedOptions);

        var hasChanged = oldName != attribute.Name ||
            oldDescription != attribute.Description ||
            oldCategory != attribute.Category ||
            OptionsChanged(oldOptions, attribute.Options);

        if (!hasChanged)
            return Result.Success(attribute.Version);

        _context.SetOriginalVersion(attribute, request.Version);
        attribute.AddDomainEvent(new AttributesChangedEvent(new Guid[] { attribute.Id }));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("The attribute was changed by another request. Reload it and try again.");
        }

        return Result.Success(attribute.Version);
    }

    private static string? ValidateOptionsForType(
        AttributeType type,
        IReadOnlyCollection<AttributeOptionInput> options)
    {
        if (type == AttributeType.Dropdown && options.Count == 0)
            return "A dropdown attribute must contain at least one option.";

        if (type != AttributeType.Dropdown && options.Count > 0)
            return "Only dropdown attributes can contain options.";

        return null;
    }

    private static string? ValidateRequestedOptionIds(
        Domain.Entities.Attribute attribute,
        IReadOnlyCollection<AttributeOptionInput> requestedOptions)
    {
        var existingIds = attribute.Options.Select(option => option.Id).ToHashSet();
        var unknownIds = requestedOptions
            .Where(option => option.Id.HasValue && !existingIds.Contains(option.Id.Value))
            .Select(option => option.Id!.Value)
            .ToArray();

        return unknownIds.Length == 0
            ? null
            : $"Options [{string.Join(", ", unknownIds)}] do not belong to this attribute.";
    }

    private static void SynchronizeDropdownOptions(
        Domain.Entities.Attribute attribute,
        IReadOnlyCollection<AttributeOptionInput> requestedOptions)
    {
        var existingOptions = attribute.Options.ToDictionary(option => option.Id);
        var requestedExistingIds = requestedOptions
            .Where(option => option.Id.HasValue)
            .Select(option => option.Id!.Value)
            .ToHashSet();

        var optionIdsToRemove = existingOptions.Keys
            .Where(id => !requestedExistingIds.Contains(id))
            .ToArray();

        attribute.RemoveOptionRange(optionIdsToRemove);

        foreach (var requestedOption in requestedOptions.Where(option => option.Id.HasValue))
            attribute.RenameOption(requestedOption.Id!.Value, requestedOption.Value);

        var optionsToAdd = requestedOptions
            .Where(option => !option.Id.HasValue)
            .Select(option => option.Value)
            .ToArray();

        attribute.AddOptionsRange(optionsToAdd);
    }

    private static bool OptionsChanged(
        IReadOnlyDictionary<Guid, string> oldOptions,
        IReadOnlyCollection<Domain.Entities.AttributeOptions> currentOptions)
    {
        if (oldOptions.Count != currentOptions.Count)
            return true;

        return currentOptions.Any(option =>
            !oldOptions.TryGetValue(option.Id, out var oldValue) || oldValue != option.Option);
    }
}
