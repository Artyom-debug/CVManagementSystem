using Application.Common.Models;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Attribute;

public sealed record CreateSystemAttributeCommand(
    string Name,
    string? Description,
    AttributeType Type,
    Category Category,
    IReadOnlyCollection<string>? Options) : IRequest<Result>;

public sealed class CreateSystemAttributeCommandValidator
    : AbstractValidator<CreateSystemAttributeCommand>
{
    public CreateSystemAttributeCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(AttributeValidationConstants.MaximumNameLength);

        RuleFor(command => command.Description)
            .MaximumLength(AttributeValidationConstants.MaximumDescriptionLength);

        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Category).IsInEnum();

        RuleFor(command => command.Options)
            .Must((command, options) => command.Type != AttributeType.Dropdown || options?.Count > 0)
            .WithMessage("A dropdown attribute must contain at least one option.")
            .Must((command, options) => command.Type == AttributeType.Dropdown || options is null || options.Count == 0)
            .WithMessage("Only dropdown attributes can contain options.")
            .Must(HaveUniqueOptions)
            .WithMessage("Option values must be unique.");

        RuleForEach(command => command.Options)
            .NotEmpty()
            .MaximumLength(AttributeValidationConstants.MaximumOptionLength);
    }

    private static bool HaveUniqueOptions(IReadOnlyCollection<string>? options) =>
        options is null || options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == options.Count(option => !string.IsNullOrWhiteSpace(option));
}

internal sealed class CreateSystemAttributeCommandHandler : IRequestHandler<CreateSystemAttributeCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public CreateSystemAttributeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(CreateSystemAttributeCommand request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim().ToUpperInvariant();
        var nameAlreadyExists = await _context.Attributes
            .AnyAsync(attribute => attribute.Name == normalizedName, cancellationToken);

        if (nameAlreadyExists)
            return Result.Failure($"Attribute '{request.Name.Trim()}' already exists.");

        var attribute = new Domain.Entities.Attribute(
            request.Name,
            request.Description,
            request.Type,
            request.Category,
            isSystem: false);

        if (request.Type == AttributeType.Dropdown)
            attribute.AddOptionsRange(request.Options ?? []);

        attribute.MarkAsSystemAttribute();
        _context.Attributes.Add(attribute);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(attribute.Version);
    }
}
