using Application.Common.Models;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Position;

public sealed record RemovePositionCommand(
    Guid PositionId,
    int Version) : IRequest<Result>;

public sealed class RemovePositionCommandValidator
    : AbstractValidator<RemovePositionCommand>
{
    public RemovePositionCommandValidator()
    {
        RuleFor(command => command.PositionId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}

internal sealed class RemovePositionCommandHandler
    : IRequestHandler<RemovePositionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public RemovePositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        RemovePositionCommand request,
        CancellationToken cancellationToken)
    {
        var position = await _context.Positions
            .SingleOrDefaultAsync(
                position => position.Id == request.PositionId,
                cancellationToken);

        if (position is null)
            return Result.Failure("Position was not found.");

        var hasCVs = await _context.CVs
            .AnyAsync(
                cv => cv.PositionId == position.Id,
                cancellationToken);

        if (hasCVs)
        {
            return Result.Failure(
                "A position with existing CVs cannot be deleted.");
        }

        _context.SetOriginalVersion(position, request.Version);
        position.AddDomainEvent(new PositionChangedEvent(position.Id));
        _context.Positions.Remove(position);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                "The position was changed by another request. Reload it and try again.");
        }
        catch (DbUpdateException)
        {
            return Result.Failure(
                "The position cannot be deleted because it is already used by a CV.");
        }

        return Result.Success();
    }
}
