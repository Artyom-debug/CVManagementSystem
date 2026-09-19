using Application.Common.Models;
using Application.Interfaces;
using Domain.Events;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PositionEntity = Domain.Entities.Position;

namespace Application.Commands.Position;

public sealed record DuplicatePositionCommand(Guid SourcePositionId, string Name) : IRequest<Result>;

public sealed class DuplicatePositionCommandValidator : AbstractValidator<DuplicatePositionCommand>
{
    public DuplicatePositionCommandValidator()
    {
        RuleFor(command => command.SourcePositionId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}

internal sealed class DuplicatePositionCommandHandler : IRequestHandler<DuplicatePositionCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public DuplicatePositionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(DuplicatePositionCommand request, CancellationToken cancellationToken)
    {
        var source = await _context.Positions
            .Include(position => position.PositionAttributes)
            .Include(position => position.Tags)
            .Include(position => position.AccessRules)
            .SingleOrDefaultAsync(position => position.Id == request.SourcePositionId, cancellationToken);

        if (source is null)
            return Result.Failure("Source position was not found.");

        var duplicate = new PositionEntity(request.Name.Trim(), source.Description ?? string.Empty, source.MaxProjectCount);

        foreach (var attribute in source.PositionAttributes.OrderBy(attribute => attribute.DisplayOrder))
        {
            duplicate.AddPositionAttribute(attribute.AttributeId, attribute.DisplayOrder);
        }

        if (source.Tags.Count > 0)
            duplicate.AddTagRange(source.Tags.ToArray());

        if (!source.IsPublic)
        {
            var accessRules = source.AccessRules
                .Select(PositionCommandModels.CloneAccessRule)
                .ToArray();

            duplicate.AddAccessRuleRange(accessRules);
        }

        duplicate.AddDomainEvent(new PositionChangedEvent(duplicate.Id));
        _context.Positions.Add(duplicate);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(duplicate.Version);
    }
}
