using Application.Common.Exceptions;
using Application.Common.Models;
using Application.Dtos;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Integrations;

public sealed record CreateSupportTicketCommand(
    string Summary,
    string Priority,
    string PageUrl,
    Guid? PositionId) : IRequest<Result>;

public sealed class CreateSupportTicketCommandValidator : AbstractValidator<CreateSupportTicketCommand>
{
    public CreateSupportTicketCommandValidator()
    {
        RuleFor(command => command.Summary)
            .NotEmpty()
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .MaximumLength(1000);

        RuleFor(command => command.Priority)
            .Must(value => value is not null &&
                           (value.Equals("High", StringComparison.OrdinalIgnoreCase) ||
                            value.Equals("Average", StringComparison.OrdinalIgnoreCase) ||
                            value.Equals("Low", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Priority must be High, Average, or Low.");

        RuleFor(command => command.PageUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                           uri.Scheme is "https" or "http" &&
                           string.IsNullOrEmpty(uri.UserInfo))
            .WithMessage("Page URL must be an absolute HTTP or HTTPS URL.");

        RuleFor(command => command.PositionId)
            .Must(id => id is null || id != Guid.Empty);
    }
}

internal sealed class CreateSupportTicketCommandHandler : IRequestHandler<CreateSupportTicketCommand, Result>
{
    private readonly IUser _user;
    private readonly IIdentityService _identity;
    private readonly IApplicationDbContext _context;
    private readonly ISupportTicketFileService _ticketFiles;

    public CreateSupportTicketCommandHandler(IUser user, IIdentityService identity, IApplicationDbContext context, ISupportTicketFileService ticketFiles)
    {
        _user = user;
        _identity = identity;
        _context = context;
        _ticketFiles = ticketFiles;
    }

    public async Task<Result> Handle(CreateSupportTicketCommand request, CancellationToken cancellationToken)
    {
        var userId = _user.Id ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var roles = _user.Roles?.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(role => role).ToArray() ?? [];
        if (roles.Length == 0)
            return Result.Failure("Current user has no assigned role.");

        var emails = await _identity.GetUserEmailsAsync([userId], cancellationToken);
        if (!emails.TryGetValue(userId, out var reporterEmail) || string.IsNullOrWhiteSpace(reporterEmail))
            return Result.Failure("Current user email was not found.");

        var adminEmails = await _identity.GetAdministratorEmailsAsync(cancellationToken);
        if (adminEmails.Count == 0)
            return Result.Failure("No administrator email address is available.");

        var positionName = "Not applicable";
        if (request.PositionId is Guid positionId)
            positionName = await _context.Positions.AsNoTracking()
                .Where(position => position.Id == positionId)
                .Select(position => position.Name)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.Position), positionId);

        var priority = request.Priority.Trim();
        priority = char.ToUpperInvariant(priority[0]) + priority[1..].ToLowerInvariant();

        var ticket = new SupportTicketFileDto(
            request.Summary.Trim(),
            $"{reporterEmail.Trim()} ({string.Join(", ", roles)})",
            positionName,
            request.PageUrl.Trim(),
            priority,
            adminEmails);

        await _ticketFiles.UploadAsync(ticket, cancellationToken);
        return Result.Success();
    }
}
