using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using Domain.Value_Objects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Profile;

public sealed record AddNewProjectCommand(
    Guid ProfileId,
    int Version,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly? EndDate,
    IReadOnlyCollection<string>? Tags) : IRequest<Result>;

public sealed class AddNewProjectCommandValidator
    : AbstractValidator<AddNewProjectCommand>
{
    public AddNewProjectCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.EndDate)
            .GreaterThanOrEqualTo(command => command.StartDate)
            .When(command => command.EndDate.HasValue)
            .WithMessage("Project end date cannot be earlier than its start date.");

        RuleFor(command => command.Tags)
            .Must(HaveUniqueTags)
            .WithMessage("Project tags must be unique.");

        RuleForEach(command => command.Tags)
            .NotEmpty()
            .MaximumLength(100);
    }

    private static bool HaveUniqueTags(IReadOnlyCollection<string>? tags) =>
        tags is null || tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == tags.Count(tag => !string.IsNullOrWhiteSpace(tag));
}

internal sealed class AddNewProjectCommandHandler
    : IRequestHandler<AddNewProjectCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public AddNewProjectCommandHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(
        AddNewProjectCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .SingleOrDefaultAsync(
                profile => profile.Id == request.ProfileId,
                cancellationToken);

        if (profile is null)
            return Result.Failure("Profile was not found.");

        var canManageProfile = profile.UserId == _user.Id ||
                               _user.Roles?.Contains(Roles.Administrator) == true;

        if (!canManageProfile)
            return Result.Failure("You do not have permission to modify this profile.");

        if (profile.Version != request.Version)
            return Result.Failure("The profile was changed by another request. Reload it and try again.");

        var period = request.EndDate.HasValue
            ? new Period(request.StartDate, request.EndDate.Value)
            : new Period(request.StartDate);

        var requestedTagNames = (request.Tags ?? [])
            .Select(tag => tag.Trim().ToUpperInvariant())
            .ToHashSet();

        var tags = await _context.Tags
            .Where(tag => requestedTagNames.Contains(tag.Name))
            .ToListAsync(cancellationToken);

        if (tags.Count != requestedTagNames.Count)
            return Result.Failure("One or more selected tags do not exist.");

        profile.AddNewProject(
            request.Name.Trim(),
            request.Description,
            period,
            tags);

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
