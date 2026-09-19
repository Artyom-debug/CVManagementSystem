using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Value_Objects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.CV;

public sealed record CreateNewCVCommand(Guid ProfileId, Guid PositionId) : IRequest<Result>;

public sealed class CreateNewCVCommandValidator : AbstractValidator<CreateNewCVCommand>
{
    public CreateNewCVCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.PositionId).NotEmpty();
    }
}

internal sealed class CreateNewCVCommandHandler : IRequestHandler<CreateNewCVCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public CreateNewCVCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(CreateNewCVCommand request, CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .AsNoTracking()
            .Include(profile => profile.AttributeValues)
            .SingleOrDefaultAsync(profile => profile.Id == request.ProfileId, cancellationToken);

        if (profile is null)
            return Result.Failure("Profile was not found.");

        var isAdministrator = _user.Roles?.Contains(Roles.Administrator) == true;
        var canManageCV = profile.UserId == _user.Id || isAdministrator;

        if (!canManageCV)
            return Result.Failure("You do not have permission to create a CV for this profile.");

        var position = await _context.Positions
            .AsNoTracking()
            .Include(position => position.AccessRules)
            .SingleOrDefaultAsync(position => position.Id == request.PositionId, cancellationToken);

        if (position is null)
            return Result.Failure("Position was not found.");


        var existingCV = await _context.CVs
            .SingleOrDefaultAsync(cv => cv.ProfileId == request.ProfileId && cv.PositionId == request.PositionId, cancellationToken);

        if (existingCV is not null &&
            !existingCV.MarkedAsDeleted)
            return Result.Failure("A CV for this position already exists.");

        var cv = new Domain.Entities.CV(request.ProfileId, request.PositionId);
        cv.AddDomainEvent(new CVChangedEvent(cv.Id, cv.ProfileId, cv.PositionId));
        _context.CVs.Add(cv);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure("A CV for this position already exists.");
        }

        return Result.Success(cv.Version);
    }

}
