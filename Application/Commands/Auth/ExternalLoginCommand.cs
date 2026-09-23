using Application.Common.Models;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Commands.Auth;

public sealed record ExternalLoginCommand(string Provider, string ProviderKey, string Email) : IRequest<AuthenticationResult>;

public sealed class ExternalLoginCommandValidator : AbstractValidator<ExternalLoginCommand>
{
    public ExternalLoginCommandValidator()
    {
        RuleFor(command => command.Provider).NotEmpty();
        RuleFor(command => command.ProviderKey).NotEmpty();
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
    }
}

internal sealed class ExternalLoginCommandHandler : IRequestHandler<ExternalLoginCommand, AuthenticationResult>
{
    private readonly IIdentityService _identityService;
    private readonly ITokenService _tokenService;

    public ExternalLoginCommandHandler(IIdentityService identityService, ITokenService tokenService)
    {
        _identityService = identityService;
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResult> Handle(ExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var (result, userId) = await _identityService.SignInWithExternalProviderAsync(request.Provider, request.ProviderKey, request.Email, cancellationToken);

        if (!result.Succeeded)
            return AuthenticationResult.Failure(result.Errors);

        var tokens = await _tokenService.GenerateTokenPairAsync(userId, cancellationToken);
        return AuthenticationResult.Success(tokens);
    }
}
