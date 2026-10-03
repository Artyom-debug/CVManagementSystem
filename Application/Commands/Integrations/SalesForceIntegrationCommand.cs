using Application.Common.Models;
using Application.Dtos;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Commands.Integrations;

public sealed record SalesForceIntegrationCommand(SalesForceDto Data) : IRequest<Result>;

public sealed class SalesForceIntegrationCommandValidator : AbstractValidator<SalesForceIntegrationCommand>
{
    public SalesForceIntegrationCommandValidator()
    {
        RuleFor(command => command.Data).NotNull();

        When(command => command.Data is not null, () =>
        {
            RuleFor(command => command.Data.OrganizationName).MaximumLength(255);
            RuleFor(command => command.Data.FirstName).NotEmpty().MaximumLength(40);
            RuleFor(command => command.Data.LastName).NotEmpty().MaximumLength(80);
            RuleFor(command => command.Data.Email).NotEmpty().EmailAddress().MaximumLength(80);
            RuleFor(command => command.Data.OrganizationPhone).MaximumLength(40);
            RuleFor(command => command.Data.OrganizationWebSite).MaximumLength(255);
            RuleFor(command => command.Data.Industry).MaximumLength(40);
            RuleFor(command => command.Data.Position).MaximumLength(128);
            RuleFor(command => command.Data.Phone).MaximumLength(40);
        });
    }
}

internal sealed class SalesForceIntegrationCommandHandler : IRequestHandler<SalesForceIntegrationCommand, Result>
{
    private readonly ISalesForceService _salesForce;

    public SalesForceIntegrationCommandHandler(ISalesForceService salesForce)
    {
        _salesForce = salesForce;
    }

    public async Task<Result> Handle(SalesForceIntegrationCommand request, CancellationToken cancellationToken)
    {
        var data = request.Data with
        {
            OrganizationName = string.IsNullOrWhiteSpace(request.Data.OrganizationName)
                ? $"Individual - {request.Data.FirstName.Trim()} {request.Data.LastName.Trim()}"
                : request.Data.OrganizationName.Trim(),
            OrganizationPhone = NormalizeOptional(request.Data.OrganizationPhone),
            OrganizationWebSite = NormalizeOptional(request.Data.OrganizationWebSite),
            Industry = NormalizeOptional(request.Data.Industry),
            FirstName = request.Data.FirstName.Trim(),
            LastName = request.Data.LastName.Trim(),
            Email = request.Data.Email.Trim(),
            Position = NormalizeOptional(request.Data.Position),
            Phone = NormalizeOptional(request.Data.Phone)
        };

        await _salesForce.CreateAccountWithContactAsync(data, cancellationToken);
        return Result.Success();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
