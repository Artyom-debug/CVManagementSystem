using Application.Interfaces;
using Domain.Abstractions;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public sealed class ApplicationDbContext : IdentityDbContext<IdentityUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Domain.Entities.Attribute> Attributes => Set<Domain.Entities.Attribute>();

    public DbSet<AttributeOptions> AttributeOptions => Set<AttributeOptions>();

    public DbSet<CV> CVs => Set<CV>();

    public DbSet<DiscussionPost> DiscussionPosts => Set<DiscussionPost>();

    public DbSet<Like> Likes => Set<Like>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<PositionAttribute> PositionAttributes => Set<PositionAttribute>();

    public DbSet<Profile> Profiles => Set<Profile>();

    public DbSet<ProfileAttributeValue> ProfileAttributes => Set<ProfileAttributeValue>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Domain.Value_Objects.Tag> Tags => Set<Domain.Value_Objects.Tag>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateEntityVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("pg_trgm");

        builder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }

    private void UpdateEntityVersions()
    {
        ChangeTracker.DetectChanges();

        var entriesToVersion = ChangeTracker.Entries()
            .Where(entry => entry.Entity is BaseEntity && entry.State == EntityState.Modified)
            .ToList();

        var changedAttributeIds = ChangeTracker.Entries<AttributeOptions>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.AttributeId)
            .ToHashSet();

        var affectedAttributeEntries = ChangeTracker.Entries<Domain.Entities.Attribute>()
            .Where(entry =>
                changedAttributeIds.Contains(entry.Entity.Id) &&
                entry.State is EntityState.Unchanged or EntityState.Modified)
            .Where(entry => entriesToVersion.All(existing => !ReferenceEquals(existing.Entity, entry.Entity)));

        entriesToVersion.AddRange(affectedAttributeEntries);

        foreach (var entry in entriesToVersion)
        {
            var versionProperty = entry.Property(nameof(BaseEntity.Version));
            versionProperty.CurrentValue = (int)versionProperty.OriginalValue! + 1;
            versionProperty.IsModified = true;
        }
    }
}
