using Application.Common.Models;
using Application.Dtos;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Attribute;

public sealed record MarkAttributeAsSystemCommand(
    Guid AttributeId,
    int Version) : IRequest<Result>;

public sealed class MarkAttributeAsSystemCommandValidator
    : AbstractValidator<MarkAttributeAsSystemCommand>
{
    public MarkAttributeAsSystemCommandValidator()
    {
        RuleFor(command => command.AttributeId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}

internal sealed class MarkAttributeAsSystemCommandHandler
    : IRequestHandler<MarkAttributeAsSystemCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public MarkAttributeAsSystemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        MarkAttributeAsSystemCommand request,
        CancellationToken cancellationToken)
    {
        var attribute = await _context.Attributes
            .SingleOrDefaultAsync(item => item.Id == request.AttributeId, cancellationToken);

        if (attribute is null)
            return Result.Failure($"Attribute '{request.AttributeId}' was not found.");

        if (attribute.IsSystem)
            return Result.Success(attribute.Version);

        _context.SetOriginalVersion(attribute, request.Version);
        attribute.MarkAsSystemAttribute();
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
}
