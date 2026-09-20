using Application.Common.Models;
using Application.Interfaces;
using Domain.Enums;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.CV;

public sealed record AddCVLikeCommand(Guid CVId) : IRequest<Result>;

public sealed class AddCVLikeCommandValidator : AbstractValidator<AddCVLikeCommand>
{
    public AddCVLikeCommandValidator()
    {
        RuleFor(command => command.CVId).NotEmpty();
    }
}

internal sealed class AddCVLikeCommandHandler : IRequestHandler<AddCVLikeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public AddCVLikeCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(AddCVLikeCommand request, CancellationToken cancellationToken)
    {
        var recruiterId = _user.Id ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var cv = await _context.CVs
            .Include(cv => cv.Likes)
            .SingleOrDefaultAsync(cv => cv.Id == request.CVId, cancellationToken);

        if (cv is null || cv.Status == Status.Deleted)
            return Result.Failure("CV was not found.");

        if (cv.Status != Status.Published)
            return Result.Failure("Only a published CV can be liked.");

        if (cv.Likes.Any(like => like.RecruterId == recruiterId))
            return Result.Success();

        cv.AddLike(recruiterId);
        cv.AddDomainEvent(new CVChangedEvent(cv.Id, cv.ProfileId, cv.PositionId));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure("Failed to like this CV");
        }

        return Result.Success();
    }
}
