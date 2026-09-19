using Application.Common.Models;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Commands.Auth;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthenticationResult>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(100);
    }
}

internal sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthenticationResult>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(IIdentityService identityService, ITokenService tokenService)
    {
        _identityService = identityService;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var (result, userId) = await _identityService.VerifyUserPasswordAsync(request.Password, request.Email, cancellationToken);

        if (!result.Succeeded)
            return AuthenticationResult.Failure(result.Errors);

        var tokens = await _tokenService.GenerateTokenPairAsync(userId, cancellationToken);

        return AuthenticationResult.Success(tokens);
    }
}
