using Application.Common.Models;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PositionEntity = Domain.Entities.Position;

namespace Application.Commands.Position;

public sealed record CreatePositionCommand(string Name, string? Description, int MaxProjectCount, bool IsPublic, IReadOnlyCollection<PositionAttributeInput> Attributes, IReadOnlyCollection<string> Tags, IReadOnlyCollection<PositionAccessRuleInput> AccessRules) : IRequest<Result>;

public sealed class CreatePositionCommandValidator : AbstractValidator<CreatePositionCommand>
{
    public CreatePositionCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Description)
            .MaximumLength(2000);

        RuleFor(command => command.MaxProjectCount)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.Attributes)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(HaveUniqueAttributeIds)
            .WithMessage("Position attribute ids must be unique.")
            .Must(HaveSequentialDisplayOrders)
            .WithMessage("Display orders must contain every value from 0 to the number of attributes minus one.");

        RuleForEach(command => command.Attributes).ChildRules(attribute =>
        {
            attribute.RuleFor(item => item.AttributeId).NotEmpty();
            attribute.RuleFor(item => item.DisplayOrder).GreaterThanOrEqualTo(0);
        });

        RuleFor(command => command.Tags)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(HaveUniqueTags)
            .WithMessage("Position tags must be unique.");

        RuleForEach(command => command.Tags)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.AccessRules)
            .NotNull()
            .Empty()
            .When(command => command.IsPublic)
            .WithMessage("A public position cannot contain access rules.");

        RuleFor(command => command.AccessRules)
            .NotEmpty()
            .When(command => !command.IsPublic)
            .WithMessage("A restricted position must contain at least one access rule.");

        RuleForEach(command => command.AccessRules).ChildRules(rule =>
        {
            rule.RuleFor(item => item.AttributeId).NotEmpty();
            rule.RuleFor(item => item.Operator).IsInEnum();
        });

        RuleFor(command => command.AccessRules)
            .Must(rules => rules is null || rules.Distinct().Count() == rules.Count)
            .WithMessage("Access rules must be unique.");
    }

    internal static bool HaveUniqueAttributeIds(IReadOnlyCollection<PositionAttributeInput> attributes) =>
        attributes.Select(attribute => attribute.AttributeId).Distinct().Count() == attributes.Count;

    internal static bool HaveSequentialDisplayOrders(IReadOnlyCollection<PositionAttributeInput> attributes) =>
        attributes.Select(attribute => attribute.DisplayOrder)
            .OrderBy(order => order)
            .SequenceEqual(Enumerable.Range(0, attributes.Count));

    internal static bool HaveUniqueTags(IReadOnlyCollection<string> tags) =>
        tags.Select(tag => tag?.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == tags.Count;
}

internal sealed class CreatePositionCommandHandler : IRequestHandler<CreatePositionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public CreatePositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(CreatePositionCommand request, CancellationToken cancellationToken)
    {
        var tagNames = request.Tags
            .Select(tag => tag.Trim().ToUpperInvariant())
            .ToHashSet();

        var tags = await _context.Tags
            .Where(tag => tagNames.Contains(tag.Name))
            .ToListAsync(cancellationToken);

        if (tags.Count != tagNames.Count)
            return Result.Failure("One or more selected tags do not exist.");

        var attributeIds = request.Attributes
            .Select(attribute => attribute.AttributeId)
            .Concat(request.AccessRules.Select(rule => rule.AttributeId))
            .ToHashSet();

        var attributes = await _context.Attributes
            .AsNoTracking()
            .Include(attribute => attribute.Options)
            .Where(attribute => attributeIds.Contains(attribute.Id))
            .ToListAsync(cancellationToken);

        if (attributes.Count != attributeIds.Count)
            return Result.Failure("One or more selected attributes do not exist.");

        var attributesById = attributes.ToDictionary(attribute => attribute.Id);

        if (!PositionCommandModels.TryCreateAccessRules(request.AccessRules, attributesById, out var accessRules, out var ruleError))
        {
            return Result.Failure(ruleError!);
        }

        var position = new PositionEntity(request.Name.Trim(), request.Description ?? string.Empty, request.MaxProjectCount);

        foreach (var attribute in request.Attributes.OrderBy(attribute => attribute.DisplayOrder))
        {
            position.AddPositionAttribute(attribute.AttributeId, attribute.DisplayOrder);
        }

        if (tags.Count > 0)
            position.AddTagRange(tags);

        if (!request.IsPublic)
            position.AddAccessRuleRange(accessRules);

        position.AddDomainEvent(new PositionChangedEvent(position.Id));

        _context.Positions.Add(position);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(position.Version);
    }
}
