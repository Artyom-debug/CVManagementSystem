using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Domain.Entities.Attribute> Attributes { get; }

    DbSet<AttributeOptions> AttributeOptions { get; }

    DbSet<CV> CVs { get; }

    DbSet<DiscussionPost> DiscussionPosts { get; }

    DbSet<Like> Likes { get; }

    DbSet<Position> Positions { get; }

    DbSet<PositionAttribute> PositionAttributes { get; }

    DbSet<Profile> Profiles { get; }

    DbSet<ProfileAttributeValue> ProfileAttributes { get; }

    DbSet<Project> Projects { get; }

    DbSet<Domain.Value_Objects.Tag> Tags { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
