using Application.Common.Models;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Commands.Auth;

public sealed record ResendConfirmationEmailCommand(string Email) : IRequest<Result>;

public sealed class ResendConfirmationEmailCommandValidator : AbstractValidator<ResendConfirmationEmailCommand>
{
    public ResendConfirmationEmailCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
    }
}

internal sealed class ResendConfirmationEmailCommandHandler : IRequestHandler<ResendConfirmationEmailCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;

    public ResendConfirmationEmailCommandHandler(IIdentityService identityService, IEmailSender emailSender)
    {
        _identityService = identityService;
        _emailSender = emailSender;
    }

    public async Task<Result> Handle(ResendConfirmationEmailCommand request, CancellationToken cancellationToken)
    {
        var (result, confirmationToken) = await _identityService.ResendConfirmationEmailAsync(request.Email, cancellationToken);

        if (!result.Succeeded || string.IsNullOrWhiteSpace(confirmationToken))
            return result;

        await _emailSender.SendConfirmationEmailAsync(request.Email.Trim(), confirmationToken, cancellationToken);
        return Result.Success();
    }
}
