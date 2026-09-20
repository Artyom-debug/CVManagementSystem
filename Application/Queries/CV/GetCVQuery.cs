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

        var cacheKey = $"cv-details:v2:{request.CVId}:user:{_user.Id}";
        var cachedCV = await _cache.GetAsync<CVDetailsDto>(cacheKey, cancellationToken);

        if (cachedCV is not null)
            return cachedCV;

        var cv = await _context.CVs
            .AsNoTracking()
            .Where(cv => cv.Id == request.CVId && cv.Status != Status.Deleted)
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

        if (!isOwner && !isAdministrator && !(isRecruiter && cv.Status == Status.Published))
            throw new ForbiddenAccessException("You do not have permission to view this CV.");

        var attributes = await _context.PositionAttributes
            .AsNoTracking()
            .Where(positionAttribute => positionAttribute.PositionId == cv.PositionId)
            .OrderBy(positionAttribute => positionAttribute.DisplayOrder)
            .Select(positionAttribute => new CVAttributeDto(
                positionAttribute.DisplayOrder,
                new DetailedAttributeDto(
                    positionAttribute.AttributeId,
                    positionAttribute.Attribute!.Version,
                    positionAttribute.Attribute.Name,
                    positionAttribute.Attribute.Description,
                    positionAttribute.Attribute.Type,
                    positionAttribute.Attribute.Category,
                    positionAttribute.Attribute.IsSystem,
                    positionAttribute.Attribute.Options
                        .OrderBy(option => option.Option)
                        .Select(option => new AttributeOptionDto(option.Id, option.Option))
                        .ToList()),
                _context.ProfileAttributes
                    .Where(value => value.ProfileId == cv.ProfileId && value.AttributeId == positionAttribute.AttributeId)
                    .Select(value => new AttributeValueDto(
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
                        value.DropdownOptionId))
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);

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
            .Append($"cv:{cv.Id}")
            .Append($"profile:{cv.ProfileId}")
            .Append($"position:{cv.PositionId}")
            .Distinct()
            .ToArray();

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), cancellationToken, dependencies);

        return result;
    }
}
