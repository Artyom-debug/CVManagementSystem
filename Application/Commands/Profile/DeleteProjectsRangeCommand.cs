using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Profile;

public sealed record DeleteProjectsRangeCommand(Guid ProfileId, IReadOnlyCollection<Guid> ProjectIds, int Version) : IRequest<Result>;

public sealed class DeleteProjectsRangeCommandValidator : AbstractValidator<DeleteProjectsRangeCommand>
{
    public DeleteProjectsRangeCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);

        RuleFor(command => command.ProjectIds)
            .NotEmpty()
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Project ids must be unique.");

        RuleForEach(command => command.ProjectIds)
            .NotEmpty();
    }
}

internal sealed class DeleteProjectsRangeCommandHandler : IRequestHandler<DeleteProjectsRangeCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public DeleteProjectsRangeCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(DeleteProjectsRangeCommand request, CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .Include(item => item.Projects)
            .SingleOrDefaultAsync(item => item.Id == request.ProfileId, cancellationToken);

        if (profile is null)
            return Result.Failure("Profile was not found.");

        var canManageProfile = profile.UserId == _user.Id ||
                               _user.Roles?.Contains(Roles.Administrator) == true;

        if (!canManageProfile)
            throw new ForbiddenAccessException("You do not have permission to manage this profile");

        var ownedProjectIds = profile.Projects
            .Select(project => project.Id)
            .ToHashSet();

        var missingProjectIds = request.ProjectIds
            .Where(id => !ownedProjectIds.Contains(id))
            .ToArray();

        if (missingProjectIds.Length > 0)
        {
            return Result.Failure($"Projects [{string.Join(", ", missingProjectIds)}] were not found in the selected profile.");
        }

        profile.DeleteProjectRange(request.ProjectIds);

        profile.AddDomainEvent(new ProfileChangedEvent(profile.Id));
        _context.SetOriginalVersion(profile, request.Version);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("The profile was changed by another request. Reload it and try again.");
        }

        return Result.Success(profile.Version);
    }
}
