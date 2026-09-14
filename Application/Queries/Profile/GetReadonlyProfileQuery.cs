using Application.Common.Exceptions;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Profile;

public sealed record GetReadonlyProfileQuery(Guid ProfileId)
    : IRequest<ReadonlyProfileDto>;

public sealed class GetReadonlyProfileQueryValidator
    : AbstractValidator<GetReadonlyProfileQuery>
{
    public GetReadonlyProfileQueryValidator()
    {
        RuleFor(query => query.ProfileId).NotEmpty();
    }
}

internal sealed class GetReadonlyProfileQueryHandler
    : IRequestHandler<GetReadonlyProfileQuery, ReadonlyProfileDto>
{
    private readonly IApplicationDbContext _context;

    public GetReadonlyProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReadonlyProfileDto> Handle(
        GetReadonlyProfileQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles
            .AsNoTracking()
            .Where(profile => profile.Id == request.ProfileId)
            .Select(profile => new
            {
                profile.Id,
                profile.CreatedAt,
                profile.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Profile), request.ProfileId);

        var attributes = await _context.ProfileAttributes
            .AsNoTracking()
            .Where(value => value.ProfileId == profile.Id)
            .OrderBy(value => value.Order)
            .Select(value => new ProfileAttributeDto(
                new AttributeValueDto(
                    value.AttributeId,
                    value.Order,
                    value.Attribute!.Type == AttributeType.String
                        ? value.StringValue
                        : value.Attribute.Type == AttributeType.Text
                            ? value.TextValue
                            : value.Attribute.Type == AttributeType.Image
                                ? value.ImageValue
                                : null,
                    value.NumericValue,
                    value.DateValue,
                    value.PeriodValue,
                    value.CheckboxValue,
                    value.DropdownOptionId),
                new DetailedAttributeDto(
                    value.Attribute.Id,
                    value.Attribute.Version,
                    value.Attribute.Name,
                    value.Attribute.Description,
                    value.Attribute.Type,
                    value.Attribute.Category,
                    value.Attribute.IsSystem,
                    value.Attribute.Options
                        .OrderBy(option => option.Option)
                        .Select(option => new AttributeOptionDto(
                            option.Id,
                            option.Option))
                        .ToList())))
            .ToListAsync(cancellationToken);

        var projects = await _context.Projects
            .AsNoTracking()
            .Where(project => project.ProfileId == profile.Id)
            .OrderBy(project => project.Name)
            .Select(project => new ProjectDto(
                project.Id,
                project.Name,
                project.Description,
                project.Period,
                project.Tags
                    .OrderBy(tag => tag.Name)
                    .Select(tag => tag.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);

        var cvs = await _context.CVs
            .AsNoTracking()
            .Where(cv => cv.ProfileId == profile.Id && cv.Status == Status.Published)
            .OrderByDescending(cv => cv.LastUpdated)
            .Select(cv => new CVDto(
                cv.Id,
                cv.PositionId,
                cv.Position!.Name,
                cv.Status,
                cv.CreatedAt,
                cv.LastUpdated,
                cv.PublishedAt))
            .ToListAsync(cancellationToken);

        return new ReadonlyProfileDto(
            profile.Id,
            profile.CreatedAt,
            profile.UpdatedAt,
            attributes,
            projects,
            cvs);
    }
}
