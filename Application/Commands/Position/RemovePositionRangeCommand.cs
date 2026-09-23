using Application.Common.Models;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Position;

public sealed record PositionVersionInput(Guid PositionId, int Version);

public sealed record RemovePositionRangeCommand(IReadOnlyCollection<PositionVersionInput> Positions) : IRequest<Result>;

public sealed class RemovePositionRangeCommandValidator : AbstractValidator<RemovePositionRangeCommand>
{
    public RemovePositionRangeCommandValidator()
    {
        RuleFor(command => command.Positions)
            .NotEmpty()
            .Must(positions => positions is null || positions.Select(position => position.PositionId).Distinct().Count() == positions.Count)
            .WithMessage("Position ids must be unique.");

        RuleForEach(command => command.Positions).ChildRules(position =>
        {
            position.RuleFor(item => item.PositionId).NotEmpty();
            position.RuleFor(item => item.Version).GreaterThanOrEqualTo(0);
        });
    }
}

internal sealed class RemovePositionRangeCommandHandler : IRequestHandler<RemovePositionRangeCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public RemovePositionRangeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(RemovePositionRangeCommand request, CancellationToken cancellationToken)
    {
        var requestedVersions = request.Positions
            .ToDictionary(position => position.PositionId, position => position.Version);

        var positions = await _context.Positions
            .Where(position => requestedVersions.Keys.Contains(position.Id))
            .ToListAsync(cancellationToken);

        if (positions.Count != requestedVersions.Count)
        {
            var foundIds = positions.Select(position => position.Id).ToHashSet();
            var missingIds = requestedVersions.Keys.Where(id => !foundIds.Contains(id));
            return Result.Failure($"Positions [{string.Join(", ", missingIds)}] were not found.");
        }

        var positionIdsWithCVs = await _context.CVs
            .AsNoTracking()
            .Where(cv => requestedVersions.Keys.Contains(cv.PositionId))
            .Select(cv => cv.PositionId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (positionIdsWithCVs.Length > 0)
            return Result.Failure($"Positions [{string.Join(", ", positionIdsWithCVs)}] cannot be deleted because they have CVs.");

        foreach (var position in positions)
        {
            _context.SetOriginalVersion(position, requestedVersions[position.Id]);
            position.AddDomainEvent(new PositionChangedEvent(position.Id));
        }

        _context.Positions.RemoveRange(positions);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("One or more positions were changed by another request. Reload them and try again.");
        }
        catch (DbUpdateException)
        {
            return Result.Failure("One or more positions cannot be deleted because they are already used by a CV.");
        }

        return Result.Success();
    }
}
