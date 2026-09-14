using Application.Common.Models;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Commands.Tag;

public sealed record AddNewTagCommand(string Name) : IRequest<Result>;

public sealed class AddNewTagCommandValidator
    : AbstractValidator<AddNewTagCommand>
{
    public AddNewTagCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}

internal sealed class AddNewTagCommandHandler
    : IRequestHandler<AddNewTagCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public AddNewTagCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(
        AddNewTagCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim().ToUpperInvariant();

        var tagExists = await _context.Tags
            .AnyAsync(tag => tag.Name == normalizedName, cancellationToken);

        if (tagExists)
            return Result.Failure($"Tag '{request.Name.Trim()}' already exists.");

        _context.Tags.Add(new Domain.Value_Objects.Tag(request.Name));
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
