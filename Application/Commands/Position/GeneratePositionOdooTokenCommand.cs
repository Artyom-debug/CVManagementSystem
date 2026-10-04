using Application.Common.Exceptions;
using Application.Common.Security;
using Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Application.Dtos;

namespace Application.Commands.Position;

public sealed record GeneratePositionOdooTokenCommand(Guid PositionId) : IRequest<PositionOdooTokenDto>;

public sealed class GeneratePositionOdooTokenCommandValidator : AbstractValidator<GeneratePositionOdooTokenCommand>
{
    public GeneratePositionOdooTokenCommandValidator() =>
        RuleFor(command => command.PositionId).NotEmpty();
}

internal sealed class GeneratePositionOdooTokenCommandHandler : IRequestHandler<GeneratePositionOdooTokenCommand, PositionOdooTokenDto>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public GeneratePositionOdooTokenCommandHandler(
        IApplicationDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PositionOdooTokenDto> Handle(GeneratePositionOdooTokenCommand request, CancellationToken cancellationToken)
    {
        var positionExists = await _context.Positions
            .AsNoTracking()
            .AnyAsync(position => position.Id == request.PositionId, cancellationToken);
        if (!positionExists)
            throw new NotFoundException(nameof(Domain.Entities.Position), request.PositionId);

        var token = PositionApiTokens.Generate();
        await _cache.SetAsync(PositionApiTokens.CacheKey(PositionApiTokens.Hash(token)), request.PositionId, TokenLifetime, cancellationToken);

        return new PositionOdooTokenDto(token, DateTime.UtcNow.Add(TokenLifetime));
    }
}
