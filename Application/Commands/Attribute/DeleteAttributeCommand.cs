using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Application.Common.Exceptions;

namespace Application.Commands.Attribute;

public sealed record DeleteAttributeCommand(Guid AttributeId, int Version) : IRequest<Result>;

public sealed class DeleteAttributeCommandValidator : AbstractValidator<DeleteAttributeCommand>
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
    private readonly IUser _user;

    public DeleteAttributeCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;

    }

    public async Task<Result> Handle(DeleteAttributeCommand request, CancellationToken cancellationToken)
    {
        var attribute = await _context.Attributes
            .SingleOrDefaultAsync(item => item.Id == request.AttributeId, cancellationToken);

        if (attribute is null)
            return Result.Failure($"Attribute '{request.AttributeId}' was not found.");

        if (attribute.IsSystem && _user.Roles?.Contains(Roles.Administrator) != true)
            throw new ForbiddenAccessException("Only an administrator can delete system attributes.");

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
