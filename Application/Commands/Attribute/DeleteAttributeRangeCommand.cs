using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Attribute;

public sealed record AttributeVersionInput(Guid AttributeId, int Version);

public sealed record DeleteAttributeRangeCommand(IReadOnlyCollection<AttributeVersionInput> Attributes) : IRequest<Result>;

public sealed class DeleteAttributeRangeCommandValidator : AbstractValidator<DeleteAttributeRangeCommand>
{
    public DeleteAttributeRangeCommandValidator()
    {
        RuleFor(command => command.Attributes)
            .NotEmpty()
            .Must(attributes => attributes is null || attributes.Select(attribute => attribute.AttributeId).Distinct().Count() == attributes.Count)
            .WithMessage("Attribute ids must be unique.");

        RuleForEach(command => command.Attributes).ChildRules(attribute =>
        {
            attribute.RuleFor(item => item.AttributeId).NotEmpty();
            attribute.RuleFor(item => item.Version).GreaterThanOrEqualTo(0);
        });
    }
}

internal sealed class DeleteAttributeRangeCommandHandler : IRequestHandler<DeleteAttributeRangeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DeleteAttributeRangeCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;

    }

    public async Task<Result> Handle(DeleteAttributeRangeCommand request, CancellationToken cancellationToken)
    {
        var requestedVersions = request.Attributes
            .ToDictionary(attribute => attribute.AttributeId, attribute => attribute.Version);

        var attributes = await _context.Attributes
            .Where(attribute => requestedVersions.Keys.Contains(attribute.Id))
            .ToListAsync(cancellationToken);

        if (attributes.Count != requestedVersions.Count)
        {
            var foundIds = attributes.Select(attribute => attribute.Id).ToHashSet();
            var missingIds = requestedVersions.Keys.Where(id => !foundIds.Contains(id));
            return Result.Failure($"Attributes [{string.Join(", ", missingIds)}] were not found.");
        }

        if (attributes.Any(attribute => attribute.IsSystem) && _user.Roles?.Contains(Roles.Administrator) != true)
            throw new ForbiddenAccessException("Only an administrator can delete system attributes.");

        foreach (var attribute in attributes)
            _context.SetOriginalVersion(attribute, requestedVersions[attribute.Id]);
        attributes[0].AddDomainEvent(new AttributesChangedEvent(attributes.Select(a =>  a.Id).ToList()));
        _context.Attributes.RemoveRange(attributes);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("One or more attributes were changed by another request. Reload them and try again.");
        }

        return Result.Success();
    }
}
