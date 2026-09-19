using Application.Common.Models;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Commands.Auth;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthenticationResult>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}

internal sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthenticationResult>
{
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    public async Task<AuthenticationResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tokens = await _tokenService.RefreshAsync(request.RefreshToken, cancellationToken);

            return AuthenticationResult.Success(tokens);
        }
        catch (UnauthorizedAccessException exception)
        {
            return AuthenticationResult.Failure(exception.Message);
        }
    }
}
