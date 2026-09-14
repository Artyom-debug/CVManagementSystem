using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.CV;

public sealed record RemoveCVCommand(
    Guid CVId,
    int Version) : IRequest<Result>;

public sealed class RemoveCVCommandValidator
    : AbstractValidator<RemoveCVCommand>
{
    public RemoveCVCommandValidator()
    {
        RuleFor(command => command.CVId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}

internal sealed class RemoveCVCommandHandler
    : IRequestHandler<RemoveCVCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public RemoveCVCommandHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(
        RemoveCVCommand request,
        CancellationToken cancellationToken)
    {
        var cv = await _context.CVs
            .Include(cv => cv.Profile)
            .SingleOrDefaultAsync(
                cv => cv.Id == request.CVId,
                cancellationToken);

        if (cv is null)
            return Result.Failure("CV was not found.");

        var canManageCV = cv.Profile!.UserId == _user.Id ||
                          _user.Roles?.Contains(Roles.Administrator) == true;

        if (!canManageCV)
            return Result.Failure("You do not have permission to remove this CV.");

        if (cv.Version != request.Version)
            return Result.Failure("The CV was changed by another request. Reload it and try again.");

        if (cv.MarkedAsDeleted)
            return Result.Failure("The CV has already been deleted.");

        cv.MarkCV();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("The CV was changed by another request. Reload it and try again.");
        }

        return Result.Success(cv.Version);
    }
}
