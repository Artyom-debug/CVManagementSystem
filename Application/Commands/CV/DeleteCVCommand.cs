using Application.Common.Models;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.CV;

public sealed record DeleteCVCommand(Guid CVId, int Version) : IRequest<Result>;

public sealed class DeleteCVCommandValidator : AbstractValidator<DeleteCVCommand>
{
    public DeleteCVCommandValidator()
    {
        RuleFor(command => command.CVId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}

internal sealed class DeleteCVCommandHandler : IRequestHandler<DeleteCVCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public DeleteCVCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(DeleteCVCommand request, CancellationToken cancellationToken)
    {
        var cv = await _context.CVs
            .SingleOrDefaultAsync(cv => cv.Id == request.CVId, cancellationToken);

        if (cv is null)
            return Result.Failure("CV was not found.");

        _context.SetOriginalVersion(cv, request.Version);
        cv.AddDomainEvent(new CVChangedEvent(cv.Id, cv.ProfileId, cv.PositionId));
        _context.CVs.Remove(cv);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("The CV was changed by another request. Reload it and try again.");
        }

        return Result.Success();
    }
}
