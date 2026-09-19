using Application.Common.Models;
using Application.Interfaces;
using MediatR;

namespace Application.Commands.Auth;

public sealed record LogoutCommand : IRequest<Result>;

internal sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IUser _user;
    private readonly ITokenService _tokenService;

    public LogoutCommandHandler(IUser user, ITokenService tokenService)
    {
        _user = user;
        _tokenService = tokenService;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            return Result.Failure("User is not authenticated.");

        await _tokenService.RevokeAllTokensAsync(_user.Id, cancellationToken);

        return Result.Success();
    }
}
