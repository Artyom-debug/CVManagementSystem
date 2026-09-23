using Application.Common.Exceptions;
using Application.Constants;
using Application.Dtos;
using Application.Interfaces;
using Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Queries.CV;

public sealed record GetCVQuery(Guid CVId) : IRequest<CVDetailsDto>;

public sealed class GetCVQueryValidator : AbstractValidator<GetCVQuery>
{
    public GetCVQueryValidator()
    {
        RuleFor(query => query.CVId).NotEmpty();
    }
}

internal sealed class GetCVQueryHandler : IRequestHandler<GetCVQuery, CVDetailsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ICacheService _cache;
    private readonly IImageStorage _imageStorage;

    public GetCVQueryHandler(IApplicationDbContext context, IUser user, ICacheService cache, IImageStorage imageStorage)
    {
        _context = context;
        _user = user;
        _cache = cache;
        _imageStorage = imageStorage;
    }

    public async Task<CVDetailsDto> Handle(GetCVQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            throw new UnauthorizedAccessException("User is not authenticated.");

        var cacheKey = $"cv-details:v3:{request.CVId}:user:{_user.Id}";
        var cachedCV = await _cache.GetAsync<CVDetailsDto>(cacheKey, cancellationToken);

        if (cachedCV is not null)
            return cachedCV;

        var cv = await _context.CVs
            .AsNoTracking()
            .Where(cv => cv.Id == request.CVId)
            .Select(cv => new
            {
                cv.Id,
                cv.Version,
                cv.ProfileId,
                ProfileVersion = cv.Profile!.Version,
                ProfileUserId = cv.Profile.UserId,
                cv.PositionId,
                PositionName = cv.Position!.Name,
                PositionDescription = cv.Position.Description,
                cv.Position.MaxProjectCount,
                PositionTags = cv.Position.Tags.Select(tag => tag.Name).ToArray(),
                cv.Status,
                cv.IsRemovedFromProfile,
                cv.CreatedAt,
                cv.PublishedAt,
                LikesCount = cv.Likes.Count,
                IsLikedByCurrentUser = cv.Likes.Any(like => like.RecruterId == _user.Id)
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.CV), request.CVId);

        var isOwner = cv.ProfileUserId == _user.Id;
        var isAdministrator = _user.Roles?.Contains(Roles.Administrator) == true;
        var isRecruiter = _user.Roles?.Contains(Roles.Recruiter) == true;
        var canOwnerView = isOwner && !cv.IsRemovedFromProfile;
        var canRecruiterView = isRecruiter && cv.Status == Status.Published;

        if (!canOwnerView && !canRecruiterView && !isAdministrator)
            throw new ForbiddenAccessException("You do not have permission to view this CV.");

        var positionOrder = await _context.PositionAttributes.AsNoTracking()
            .Where(x => x.PositionId == cv.PositionId)
            .ToDictionaryAsync(x => x.AttributeId, x => x.DisplayOrder, cancellationToken);

        var cvAttributesQuery = _context.Attributes
            .AsNoTracking()
            .Where(attribute =>
                attribute.IsSystem ||
                _context.PositionAttributes.Any(positionAttribute =>
                    positionAttribute.PositionId == cv.PositionId &&
                    positionAttribute.AttributeId == attribute.Id));

        var attributes = await cvAttributesQuery
            .Select(attribute => new CVAttributeDto(
                0,
                new DetailedAttributeDto(
                    attribute.Id,
                    attribute.Version,
                    attribute.Name,
                    attribute.Description,
                    attribute.Type,
                    attribute.Category,
                    attribute.IsSystem,
                    attribute.Options
                        .OrderBy(option => option.Option)
                        .Select(option => new AttributeOptionDto(option.Id, option.Option))
                        .ToList()),
                _context.ProfileAttributes
                    .Where(value => value.ProfileId == cv.ProfileId && value.AttributeId == attribute.Id)
                    .Select(value => new AttributeValueDto(
                        value.AttributeId,
                        value.Order,
                        attribute.Type == AttributeType.String
                            ? value.StringValue
                            : attribute.Type == AttributeType.Text
                                ? value.TextValue
                                : attribute.Type == AttributeType.Image
                                    ? value.ImageValue
                                    : null,
                        value.NumericValue,
                        value.DateValue,
                        value.PeriodValue,
                        value.CheckboxValue,
                        value.DropdownOptionId))
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);

        attributes = attributes.OrderByDescending(x => x.Attribute.IsSystem)
            .ThenBy(x => x.Attribute.IsSystem ? 0 : positionOrder.GetValueOrDefault(x.Attribute.Id))
            .Select((x, index) => x with { DisplayOrder = index }).ToList();

        attributes = attributes
            .Select(attribute =>
            {
                var value = attribute.Value;

                if (attribute.Attribute.Type != AttributeType.Image ||
                    value?.StringValue is not string publicId ||
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

        List<ProjectDto> projects = [];

        if (cv.MaxProjectCount > 0)
        {
            var projectsQuery = _context.Projects
                .AsNoTracking()
                .Where(project => project.ProfileId == cv.ProfileId);

            if (cv.PositionTags.Length > 0)
                projectsQuery = projectsQuery.Where(project => project.Tags.Any(tag => cv.PositionTags.Contains(tag.Name)));

            projects = await projectsQuery
                .OrderByDescending(project => project.Period.Start)
                .Take(cv.MaxProjectCount)
                .Select(project => new ProjectDto(project.Id, project.Name, project.Description, project.Period, project.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToList()))
                .ToListAsync(cancellationToken);
        }

        var result = new CVDetailsDto(cv.Id, cv.Version, cv.ProfileId, cv.ProfileVersion, cv.PositionId, cv.PositionName, cv.PositionDescription, cv.Status, cv.CreatedAt, cv.PublishedAt, attributes, projects, cv.LikesCount, cv.IsLikedByCurrentUser);

        var dependencies = attributes
            .Select(attribute => $"attribute:{attribute.Attribute.Id}")
            .Append("attribute-library")
            .Append($"cv:{cv.Id}")
            .Append($"profile:{cv.ProfileId}")
            .Append($"position:{cv.PositionId}")
            .Distinct()
            .ToArray();

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), cancellationToken, dependencies);

        return result;
    }
}
