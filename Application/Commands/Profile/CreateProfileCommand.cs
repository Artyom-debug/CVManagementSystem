using Application.Common.Models;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProfileEntity = Domain.Entities.Profile;

namespace Application.Commands;

public sealed record CreateProfileCommand(string UserId) : IRequest<Result>;

public sealed class CreateProfileCommandValidator : AbstractValidator<CreateProfileCommand>
{
    public CreateProfileCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty()
            .MaximumLength(450);
    }
}

internal sealed class CreateProfileCommandHandler : IRequestHandler<CreateProfileCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public CreateProfileCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(CreateProfileCommand request, CancellationToken cancellationToken)
    {
        var profileExists = await _context.Profiles
            .AnyAsync(profile => profile.UserId == request.UserId, cancellationToken);

        if (profileExists)
            return Result.Failure("A profile for this user already exists.");

        var profile = new ProfileEntity(request.UserId);

        await _context.Profiles.AddAsync(profile, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(profile.Version);
    }
}
