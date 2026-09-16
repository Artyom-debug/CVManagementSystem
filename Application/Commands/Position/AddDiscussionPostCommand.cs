using Application.Common.Models;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Position;

public sealed record AddDiscussionPostCommand(
    Guid PositionId,
    string Content) : IRequest<Result>;

public sealed class AddDiscussionPostCommandValidator
    : AbstractValidator<AddDiscussionPostCommand>
{
    public AddDiscussionPostCommandValidator()
    {
        RuleFor(command => command.PositionId).NotEmpty();
        RuleFor(command => command.Content)
            .Must(content => !string.IsNullOrWhiteSpace(content))
            .WithMessage("Discussion post content cannot be empty.");
    }
}

internal sealed class AddDiscussionPostCommandHandler
    : IRequestHandler<AddDiscussionPostCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public AddDiscussionPostCommandHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(
        AddDiscussionPostCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _user.Id
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var position = await _context.Positions
            .Include(position => position.DiscussionPosts)
            .SingleOrDefaultAsync(
                position => position.Id == request.PositionId,
                cancellationToken);

        if (position is null)
            return Result.Failure("Position was not found.");

        position.AddDiscussionPost(userId, request.Content.Trim());
        position.AddDomainEvent(new PositionChangedEvent(position.Id));

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
