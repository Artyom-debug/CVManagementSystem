using Application.Common.Models;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Attribute;

public sealed record DeleteAttributeCommand(
    Guid AttributeId,
    int Version) : IRequest<Result>;

public sealed class DeleteAttributeCommandValidator
    : AbstractValidator<DeleteAttributeCommand>
{
    public DeleteAttributeCommandValidator()
    {
        RuleFor(command => command.AttributeId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}

internal sealed class DeleteAttributeCommandHandler : IRequestHandler<DeleteAttributeCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public DeleteAttributeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(DeleteAttributeCommand request, CancellationToken cancellationToken)
    {
        var attribute = await _context.Attributes
            .SingleOrDefaultAsync(item => item.Id == request.AttributeId, cancellationToken);

        if (attribute is null)
            return Result.Failure($"Attribute '{request.AttributeId}' was not found.");

        if (attribute.IsSystem)
            return Result.Failure("Use the system attribute operation to delete a system attribute.");

        _context.SetOriginalVersion(attribute, request.Version);
        attribute.AddDomainEvent(new AttributesChangedEvent(new Guid[] {attribute.Id}));
        _context.Attributes.Remove(attribute);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("The attribute was changed by another request. Reload it and try again.");
        }

        return Result.Success();
    }
}
