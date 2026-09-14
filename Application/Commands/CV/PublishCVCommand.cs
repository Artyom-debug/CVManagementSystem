using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.CV;

public sealed record PublishCVCommand(
    Guid CVId,
    int Version) : IRequest<Result>;

public sealed class PublishCVCommandValidator
    : AbstractValidator<PublishCVCommand>
{
    public PublishCVCommandValidator()
    {
        RuleFor(command => command.CVId).NotEmpty();
        RuleFor(command => command.Version).GreaterThanOrEqualTo(0);
    }
}

internal sealed class PublishCVCommandHandler
    : IRequestHandler<PublishCVCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public PublishCVCommandHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<Result> Handle(
        PublishCVCommand request,
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
            return Result.Failure("You do not have permission to publish this CV.");

        if (cv.Version != request.Version)
            return Result.Failure("The CV was changed by another request. Reload it and try again.");

        if (cv.Status == Status.Published)
            return Result.Failure("The CV has already been published.");

        if (cv.Status == Status.Deleted)
            return Result.Failure("A deleted CV cannot be published.");

        var positionAttributes = await _context.PositionAttributes
            .AsNoTracking()
            .Where(item => item.PositionId == cv.PositionId)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => new
            {
                item.AttributeId,
                item.Attribute!.Name,
                item.Attribute.Type
            })
            .ToListAsync(cancellationToken);

        var positionAttributeIds = positionAttributes
            .Select(attribute => attribute.AttributeId)
            .ToArray();

        var profileValues = await _context.ProfileAttributes
            .AsNoTracking()
            .Where(value =>
                value.ProfileId == cv.ProfileId &&
                positionAttributeIds.Contains(value.AttributeId))
            .ToDictionaryAsync(
                value => value.AttributeId,
                cancellationToken);

        var unfilledAttributes = positionAttributes
            .Where(attribute =>
                !profileValues.TryGetValue(attribute.AttributeId, out var profileValue) ||
                !HasValue(profileValue, attribute.Type))
            .Select(attribute => attribute.Name)
            .ToArray();

        if (unfilledAttributes.Length > 0)
        {
            return Result.Failure(unfilledAttributes.Select(name =>
                $"Attribute '{name}' must be filled before the CV can be published."));
        }

        cv.Publish();

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

    private static bool HasValue(ProfileAttributeValue profileValue, AttributeType type)
    {
        var value = profileValue.GetAttributeValue(type);

        return value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            Guid id => id != Guid.Empty,
            _ => true
        };
    }
}
