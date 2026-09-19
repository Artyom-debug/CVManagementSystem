using Application.Common.Models;
using Application.Constants;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Commands.Auth;

public sealed record BlockUserCommand(string UserId) : IRequest<Result>;

public sealed record UnblockUserCommand(string UserId) : IRequest<Result>;

public sealed record DeleteUserCommand(string UserId) : IRequest<Result>;

public sealed record AddUserToRoleCommand(string UserId, string Role) : IRequest<Result>;

public sealed record RemoveUserFromRoleCommand(string UserId, string Role) : IRequest<Result>;

public sealed class BlockUserCommandValidator : AbstractValidator<BlockUserCommand>
{
    public BlockUserCommandValidator() =>
        RuleFor(command => command.UserId).NotEmpty();
}

public sealed class UnblockUserCommandValidator : AbstractValidator<UnblockUserCommand>
{
    public UnblockUserCommandValidator() =>
        RuleFor(command => command.UserId).NotEmpty();
}

public sealed class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserCommandValidator() =>
        RuleFor(command => command.UserId).NotEmpty();
}

public sealed class AddUserToRoleCommandValidator : AbstractValidator<AddUserToRoleCommand>
{
    public AddUserToRoleCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Role)
            .Must(IsSupportedRole)
            .WithMessage("Unknown user role.");
    }

    private static bool IsSupportedRole(string role) =>
        role is Roles.Candidate or Roles.Recruiter or Roles.Administrator;
}

public sealed class RemoveUserFromRoleCommandValidator : AbstractValidator<RemoveUserFromRoleCommand>
{
    public RemoveUserFromRoleCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Role)
            .Must(IsSupportedRole)
            .WithMessage("Unknown user role.");
    }

    private static bool IsSupportedRole(string role) =>
        role is Roles.Candidate or Roles.Recruiter or Roles.Administrator;
}

internal sealed class BlockUserCommandHandler : IRequestHandler<BlockUserCommand, Result>
{
    private readonly IIdentityService _identityService;

    public BlockUserCommandHandler(IIdentityService identityService) =>
        _identityService = identityService;

    public Task<Result> Handle(BlockUserCommand request, CancellationToken cancellationToken) =>
        _identityService.BlockUserAsync(request.UserId, cancellationToken);
}

internal sealed class UnblockUserCommandHandler : IRequestHandler<UnblockUserCommand, Result>
{
    private readonly IIdentityService _identityService;

    public UnblockUserCommandHandler(IIdentityService identityService) =>
        _identityService = identityService;

    public Task<Result> Handle(UnblockUserCommand request, CancellationToken cancellationToken) =>
        _identityService.UnblockUserAsync(request.UserId, cancellationToken);
}

internal sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result>
{
    private readonly IIdentityService _identityService;

    public DeleteUserCommandHandler(IIdentityService identityService) =>
        _identityService = identityService;

    public Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken) =>
        _identityService.DeleteUserAsync(request.UserId, cancellationToken);
}

internal sealed class AddUserToRoleCommandHandler : IRequestHandler<AddUserToRoleCommand, Result>
{
    private readonly IIdentityService _identityService;

    public AddUserToRoleCommandHandler(IIdentityService identityService) =>
        _identityService = identityService;

    public Task<Result> Handle(AddUserToRoleCommand request, CancellationToken cancellationToken) =>
        _identityService.AddUserToRoleAsync(request.UserId, request.Role, cancellationToken);
}

internal sealed class RemoveUserFromRoleCommandHandler : IRequestHandler<RemoveUserFromRoleCommand, Result>
{
    private readonly IIdentityService _identityService;

    public RemoveUserFromRoleCommandHandler(IIdentityService identityService) =>
        _identityService = identityService;

    public Task<Result> Handle(RemoveUserFromRoleCommand request, CancellationToken cancellationToken) =>
        _identityService.RemoveUserFromRoleAsync(request.UserId, request.Role, cancellationToken);
}
