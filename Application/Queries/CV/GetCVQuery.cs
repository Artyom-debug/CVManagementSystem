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

internal sealed class GetCVQueryHandler
    : IRequestHandler<GetCVQuery, CVDetailsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;
    private readonly ICacheService _cache;

    public GetCVQueryHandler(
        IApplicationDbContext context,
        IUser user,
        ICacheService cache)
    {
        _context = context;
        _user = user;
        _cache = cache;
    }

    public async Task<CVDetailsDto> Handle(
        GetCVQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_user.Id))
            throw new UnauthorizedAccessException("User is not authenticated.");

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
                cv.LastUpdated,
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

        var cacheKey = $"cv-details:v1:{cv.Id}:user:{_user.Id}";
        var cachedCV = await _cache.GetAsync<CVDetailsDto>(
            cacheKey,
            cancellationToken);

        if (cachedCV is not null)
            return cachedCV;

        var attributes = await _context.PositionAttributes
            .AsNoTracking()
            .Where(p => p.PositionId == cv.PositionId)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new CVAttributeDto(
                p.DisplayOrder,
                new DetailedAttributeDto(
                    p.AttributeId,
                    p.Attribute!.Version,
                    p.Attribute.Name,
                    p.Attribute.Description,
                    p.Attribute.Type,
                    p.Attribute.Category,
                    p.Attribute.IsSystem,
                    p.Attribute.Options
                        .OrderBy(option => option.Option)
                        .Select(option => new AttributeOptionDto(
                            option.Id,
                            option.Option))
                        .ToList()),
                _context.ProfileAttributes
                    .Where(pv =>
                        pv.ProfileId == cv.ProfileId &&
                        pv.AttributeId == p.AttributeId)
                    .Select(pv => new AttributeValueDto(
                        pv.AttributeId,
                        pv.Order,
                        pv.Attribute!.Type == AttributeType.String
                            ? pv.StringValue
                            : pv.Attribute.Type == AttributeType.Text
                                ? pv.TextValue
                                : pv.Attribute.Type == AttributeType.Image
                                    ? pv.ImageValue
                                    : null,
                        pv.NumericValue,
                        pv.DateValue,
                        pv.PeriodValue,
                        pv.CheckboxValue,
                        pv.DropdownOptionId))
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);

        List<ProjectDto> projects = [];

        if (cv.MaxProjectCount > 0)
        {
            var projectsQuery = _context.Projects
                .AsNoTracking()
                .Where(project => project.ProfileId == cv.ProfileId);

            if (cv.PositionTags.Length > 0)
            {
                projectsQuery = projectsQuery.Where(project =>
                    project.Tags.Any(tag => cv.PositionTags.Contains(tag.Name)));
            }

            projects = await projectsQuery
                .OrderByDescending(project => project.Period.Start)
                .Take(cv.MaxProjectCount)
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
        }

        var result = new CVDetailsDto(
            cv.Id,
            cv.Version,
            cv.ProfileId,
            cv.ProfileVersion,
            cv.PositionId,
            cv.PositionName,
            cv.PositionDescription,
            cv.Status,
            cv.CreatedAt,
            cv.LastUpdated,
            cv.PublishedAt,
            attributes,
            projects,
            cv.LikesCount,
            cv.IsLikedByCurrentUser);

        var dependencies = attributes
            .Select(attribute => $"attribute:{attribute.Attribute.Id}")
            .Append($"cv:{cv.Id}")
            .Append($"profile:{cv.ProfileId}")
            .Append($"position:{cv.PositionId}")
            .Distinct()
            .ToArray();

        await _cache.SetAsync(
            cacheKey,
            result,
            TimeSpan.FromMinutes(10),
            cancellationToken,
            dependencies);

        return result;
    }
}
