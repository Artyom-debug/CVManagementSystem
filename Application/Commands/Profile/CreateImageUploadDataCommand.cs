using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Profile;

public sealed record CreateImageUploadDataCommand(Guid ProfileId, Guid AttributeId) : IRequest<ImageUploadData>;

public sealed class CreateImageUploadDataCommandValidator : AbstractValidator<CreateImageUploadDataCommand>
{
    public CreateImageUploadDataCommandValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.AttributeId).NotEmpty();
    }
}

internal sealed class CreateImageUploadDataCommandHandler : IRequestHandler<CreateImageUploadDataCommand, ImageUploadData>
{
    private readonly IApplicationDbContext _context;
    private readonly IImageStorage _imageStorage;
    private readonly IUser _user;

    public CreateImageUploadDataCommandHandler(IApplicationDbContext context, IImageStorage imageStorage, IUser user)
    {
        _context = context;
        _imageStorage = imageStorage;
        _user = user;
    }

    public async Task<ImageUploadData> Handle(CreateImageUploadDataCommand request, CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.Id == request.ProfileId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Profile), request.ProfileId);

        var canManageProfile = profile.UserId == _user.Id ||
                               _user.Roles?.Contains(Roles.Administrator) == true;

        if (!canManageProfile)
            throw new ForbiddenAccessException("You do not have permission to manage this profile.");

        var attribute = await _context.Attributes
            .AsNoTracking()
            .SingleOrDefaultAsync(attribute => attribute.Id == request.AttributeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Attribute), request.AttributeId);

        if (attribute.Type != AttributeType.Image)
            throw new InvalidOperationException("Upload data can only be created for an image attribute.");

        return _imageStorage.CreateUploadData(profile.Id, attribute.Id);
    }
}
