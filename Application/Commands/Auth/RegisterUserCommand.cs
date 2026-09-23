using Application.Common.Models;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Commands.Auth;

public sealed record RegisterUserCommand(string Email, string Password) : IRequest<Result>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(100);
    }
}

internal sealed class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result>
{
    private readonly IIdentityService _identityService;
    private readonly IEmailSender _emailSender;

    public RegisterUserCommandHandler(IIdentityService identityService, IEmailSender emailSender)
    {
        _identityService = identityService;
        _emailSender = emailSender;
    }

    public async Task<Result> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var (result, confirmationToken) = await _identityService.StartRegistrationAsync(request.Password, request.Email, cancellationToken);

        if (!result.Succeeded)
            return result;

        if (string.IsNullOrWhiteSpace(confirmationToken))
            return Result.Failure("Could not start email confirmation. Please try again.");

        await _emailSender.SendConfirmationEmailAsync(request.Email.Trim(), confirmationToken, cancellationToken);

        return Result.Success();
    }
}
