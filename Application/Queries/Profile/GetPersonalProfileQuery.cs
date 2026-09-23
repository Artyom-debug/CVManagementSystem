using Application.Common.Exceptions;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.Profile;

public sealed record GetPersonalProfileQuery : IRequest<ProfileDto>;

internal sealed class GetPersonalProfileQueryHandler : IRequestHandler<GetPersonalProfileQuery, ProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ICacheService _cache;
    private readonly IImageStorage _imageStorage;

    public GetPersonalProfileQueryHandler(IApplicationDbContext context, IUser user, ICacheService cache, IImageStorage imageStorage)
    {
        _context = context;
        _user = user;
        _cache = cache;
        _imageStorage = imageStorage;
    }

    public async Task<ProfileDto> Handle(GetPersonalProfileQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var systemOnly = _user.Roles?.Contains(Roles.Recruiter) == true &&
                         _user.Roles.Contains(Roles.Candidate) == false &&
                         _user.Roles.Contains(Roles.Administrator) == false;
        var cacheKey = $"profile-personal:v3:user:{_user.Id}:system-only:{systemOnly}";
        var cachedProfile = await _cache.GetAsync<ProfileDto>(cacheKey, cancellationToken);

        if (cachedProfile is not null)
            return cachedProfile;

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
            .Where(value =>
                value.ProfileId == profile.Id &&
                (!systemOnly || value.Attribute!.IsSystem))
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

        attributes = attributes
            .Select(attribute =>
            {
                var value = attribute.Value;

                if (attribute.Attribute.Type != AttributeType.Image ||
                    value.StringValue is not string publicId ||
                    string.IsNullOrWhiteSpace(publicId))
                {
                    return attribute;
                }

                return attribute with
                {
                    Value = value with
                    {
                        StringValue = _imageStorage.CreateUrl(publicId)
                    }
                };
            })
            .ToList();

        var projects = systemOnly
            ? []
            : await _context.Projects
                .AsNoTracking()
                .Where(project => project.ProfileId == profile.Id)
                .OrderBy(project => project.Period.Start)
                .Select(project => new ProjectDto(project.Id, project.Name, project.Description, project.Period, project.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToList()))
                .ToListAsync(cancellationToken);

        var cvs = systemOnly
            ? []
            : await _context.CVs
                .AsNoTracking()
                .Where(cv =>
                    cv.ProfileId == profile.Id &&
                    !cv.IsRemovedFromProfile)
                .OrderByDescending(cv => cv.CreatedAt)
                .Select(cv => new CVDto(cv.Id, cv.PositionId, cv.Position!.Name, cv.Status, cv.CreatedAt, cv.PublishedAt))
                .ToListAsync(cancellationToken);

        var result = new ProfileDto(profile.Id, profile.Version, profile.CreatedAt, profile.UpdatedAt, attributes, projects, cvs);

        var dependencies = attributes
            .Select(attribute => $"attribute:{attribute.Attribute.Id}")
            .Append($"profile:{profile.Id}")
            .Concat(cvs.Select(cv => $"position:{cv.PositionId}"))
            .Distinct()
            .ToArray();

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15), cancellationToken, dependencies);

        return result;
    }
}
