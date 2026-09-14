using Application.Common.Exceptions;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Profile;

public sealed record GetPersonalProfileQuery : IRequest<ProfileDto>;

internal sealed class GetPersonalProfileQueryHandler
    : IRequestHandler<GetPersonalProfileQuery, ProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GetPersonalProfileQueryHandler(
        IApplicationDbContext context,
        IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<ProfileDto> Handle(
        GetPersonalProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var profile = await _context.Profiles
            .AsNoTracking()
            .Where(profile => profile.UserId == _user.Id)
            .Select(profile => new
            {
                profile.Id,
                profile.Version,
                profile.CreatedAt,
                profile.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Profile), _user.Id);

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
            .OrderBy(project => project.Period.Start)
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
            .Where(cv => cv.ProfileId == profile.Id && cv.Status != Status.Deleted)
            .OrderByDescending(cv => cv.CreatedAt)
            .Select(cv => new CVDto(
                cv.Id,
                cv.PositionId,
                cv.Position!.Name,
                cv.Status,
                cv.CreatedAt,
                cv.LastUpdated,
                cv.PublishedAt))
            .ToListAsync(cancellationToken);

        return new ProfileDto(
            profile.Id,
            profile.Version,
            profile.CreatedAt,
            profile.UpdatedAt,
            attributes,
            projects,
            cvs);
    }
}
