using Application.Common.Models;
using Application.Interfaces;
using Domain.Enums;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.CV;

public sealed record RemoveCVLikeCommand(Guid CVId) : IRequest<Result>;

public sealed class RemoveCVLikeCommandValidator
    : AbstractValidator<RemoveCVLikeCommand>
{
    public RemoveCVLikeCommandValidator()
    {
        RuleFor(command => command.CVId).NotEmpty();
    }
}

internal sealed class RemoveCVLikeCommandHandler
    : IRequestHandler<RemoveCVLikeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public RemoveCVLikeCommandHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(
        RemoveCVLikeCommand request,
        CancellationToken cancellationToken)
    {
        var recruiterId = _user.Id
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var cv = await _context.CVs
            .Include(cv => cv.Likes)
            .SingleOrDefaultAsync(
                cv => cv.Id == request.CVId,
                cancellationToken);

        if (cv is null || cv.Status == Status.Deleted)
            return Result.Failure("CV was not found.");

        if (cv.Likes.All(like => like.RecruterId != recruiterId))
            return Result.Success();

        cv.RemoveLike(recruiterId);
        cv.AddDomainEvent(new CVChangedEvent(
            cv.Id,
            cv.ProfileId,
            cv.PositionId));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Success();
        }

        return Result.Success();
    }
}
