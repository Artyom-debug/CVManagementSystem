using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using Domain.Events;
using Domain.Value_Objects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Application.Common.Exceptions;

namespace Application.Commands.Profile;

public sealed record UpdateProjectInfoCommand(Guid ProfileId, Guid ProjectId, int Version, string Name, string? Description, DateOnly StartDate, DateOnly? EndDate, IReadOnlyCollection<string>? Tags) : IRequest<Result>;

public sealed class UpdateProjectInfoCommandValidator : AbstractValidator<UpdateProjectInfoCommand>
{
    public UpdateProjectInfoCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.ProjectId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.EndDate)
            .GreaterThanOrEqualTo(command => command.StartDate)
            .When(command => command.EndDate.HasValue)
            .WithMessage("Project end date cannot be earlier than its start date.");

        RuleFor(command => command.Tags)
            .Must(tags => tags is null || tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == tags.Count(tag => !string.IsNullOrWhiteSpace(tag)))
            .WithMessage("Project tags must be unique.");

        RuleForEach(command => command.Tags)
            .NotEmpty()
            .MaximumLength(100);
    }
}

internal sealed class UpdateProjectInfoCommandHandler : IRequestHandler<UpdateProjectInfoCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public UpdateProjectInfoCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(UpdateProjectInfoCommand request, CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .Include(profile => profile.Projects)
            .ThenInclude(project => project.Tags)
            .SingleOrDefaultAsync(profile => profile.Id == request.ProfileId, cancellationToken);

        if (profile is null)
            return Result.Failure("Profile was not found.");

        var canManageProfile = profile.UserId == _user.Id ||
                               _user.Roles?.Contains(Roles.Administrator) == true;

        if (!canManageProfile)
            throw new ForbiddenAccessException("You do not have permission to manage this profile");

        var project = profile.Projects
            .SingleOrDefault(project => project.Id == request.ProjectId);

        if (project is null)
            return Result.Failure($"Project '{request.ProjectId}' was not found.");

        var period = request.EndDate.HasValue
            ? new Period(request.StartDate, request.EndDate.Value)
            : new Period(request.StartDate);

        var requestedTags = (request.Tags ?? [])
            .Select(tag => tag.Trim().ToUpperInvariant())
            .ToHashSet();

        var currentTags = project.Tags
            .Select(tag => tag.Name)
            .ToHashSet();

        var tagNamesToAdd = requestedTags.Except(currentTags).ToArray();
        var tagsToAdd = await _context.Tags
            .Where(tag => tagNamesToAdd.Contains(tag.Name))
            .ToListAsync(cancellationToken);

        if (tagsToAdd.Count != tagNamesToAdd.Length)
            return Result.Failure("One or more selected tags do not exist.");

        var oldName = project.Name;
        var oldDescription = project.Description;
        var oldPeriod = project.Period;
        var oldTags = project.Tags
            .Select(tag => tag.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasChanged =
            oldName != project.Name ||
            oldDescription != project.Description ||
            oldPeriod != project.Period ||
            !oldTags.SetEquals(project.Tags.Select(tag => tag.Name));

        if (!hasChanged)
            return Result.Success(profile.Version);

        profile.RenameProject(project.Id, request.Name.Trim());
        profile.SetProjectDescription(project.Id, request.Description);
        profile.SetProjectPeriod(project.Id, period);
        profile.RemoveProjectTagRange(project.Id, currentTags.Except(requestedTags).ToArray());
        profile.AddProjectTagRange(project.Id, tagsToAdd);

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
