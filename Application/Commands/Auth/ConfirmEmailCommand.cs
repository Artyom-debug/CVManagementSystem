using Application.Common.Models;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Commands.Auth;

public sealed record ConfirmEmailCommand(string Token) : IRequest<Result>;

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(command => command.Token).NotEmpty();
    }
}

internal sealed class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, Result>
{
    private readonly IIdentityService _identityService;

    public ConfirmEmailCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken) =>
        _identityService.ConfirmEmailAsync(request.Token, cancellationToken);
}
