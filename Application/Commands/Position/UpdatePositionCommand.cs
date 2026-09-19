using Application.Common.Models;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Position;

public sealed record UpdatePositionCommand(Guid PositionId, int Version, string Name, string? Description, int MaxProjectCount, bool IsPublic, IReadOnlyCollection<PositionAttributeInput> Attributes, IReadOnlyCollection<string> Tags, IReadOnlyCollection<PositionAccessRuleInput> AccessRules) : IRequest<Result>;

public sealed class UpdatePositionCommandValidator : AbstractValidator<UpdatePositionCommand>
{
    public UpdatePositionCommandValidator()
    {
        RuleFor(command => command.PositionId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);

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
            .Must(CreatePositionCommandValidator.HaveUniqueAttributeIds)
            .WithMessage("Position attribute ids must be unique.")
            .Must(CreatePositionCommandValidator.HaveSequentialDisplayOrders)
            .WithMessage("Display orders must contain every value from 0 to the number of attributes minus one.");

        RuleForEach(command => command.Attributes).ChildRules(attribute =>
        {
            attribute.RuleFor(item => item.AttributeId).NotEmpty();
            attribute.RuleFor(item => item.DisplayOrder).GreaterThanOrEqualTo(0);
        });

        RuleFor(command => command.Tags)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(CreatePositionCommandValidator.HaveUniqueTags)
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
}

internal sealed class UpdatePositionCommandHandler : IRequestHandler<UpdatePositionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public UpdatePositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdatePositionCommand request, CancellationToken cancellationToken)
    {
        var position = await _context.Positions
            .Include(position => position.PositionAttributes)
            .Include(position => position.Tags)
            .Include(position => position.AccessRules)
            .SingleOrDefaultAsync(position => position.Id == request.PositionId, cancellationToken);

        if (position is null)
            return Result.Failure("Position was not found.");

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

        if (!PositionCommandModels.TryCreateAccessRules(request.AccessRules, attributes.ToDictionary(attribute => attribute.Id), out var accessRules, out var ruleError))
        {
            return Result.Failure(ruleError!);
        }

        position.RenamePosition(request.Name.Trim());
        position.AddDescription(request.Description ?? string.Empty);
        position.SetProjectCount(request.MaxProjectCount);

        var currentAttributeIds = position.PositionAttributes
            .Select(attribute => attribute.AttributeId)
            .ToArray();

        if (currentAttributeIds.Length > 0)
            position.RemovePositionAttributeRange(currentAttributeIds);

        foreach (var attribute in request.Attributes.OrderBy(attribute => attribute.DisplayOrder))
        {
            position.AddPositionAttribute(attribute.AttributeId, attribute.DisplayOrder);
        }

        var requestedTagNames = tags.Select(tag => tag.Name).ToHashSet();
        var tagNamesToRemove = position.Tags
            .Where(tag => !requestedTagNames.Contains(tag.Name))
            .Select(tag => tag.Name)
            .ToArray();

        if (tagNamesToRemove.Length > 0)
            position.RemoveTagRange(tagNamesToRemove);

        var currentTagNames = position.Tags.Select(tag => tag.Name).ToHashSet();
        var tagsToAdd = tags.Where(tag => !currentTagNames.Contains(tag.Name)).ToArray();

        if (tagsToAdd.Length > 0)
            position.AddTagRange(tagsToAdd);

        var currentRules = position.AccessRules.ToArray();
        if (currentRules.Length > 0)
            position.RemoveAccessRuleRange(currentRules);

        if (!request.IsPublic)
            position.AddAccessRuleRange(accessRules);

        position.AddDomainEvent(new PositionChangedEvent(position.Id));
        _context.SetOriginalVersion(position, request.Version);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("The position was changed by another request. Reload it and try again.");
        }

        return Result.Success(position.Version);
    }
}
