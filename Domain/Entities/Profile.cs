using Domain.Abstractions;
using Domain.Enums;
using Domain.Value_Objects;

namespace Domain.Entities;

public sealed class Profile : BaseEntity
{
    public string UserId { get; private set; }
    
    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private readonly List<ProfileAttributeValue> _attributeValues = new();

    private readonly List<Project> _projects = new();

    public IReadOnlyCollection<ProfileAttributeValue> AttributeValues => _attributeValues.AsReadOnly();

    public IReadOnlyCollection<Project> Projects => _projects.AsReadOnly();

    public Profile(string userId)
    {
        if(string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User not found", nameof(userId));
        UserId = userId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void DeleteProjectRange(IReadOnlyCollection<Guid> projectIds)
    {
        ArgumentNullException.ThrowIfNull(projectIds);
        if (projectIds.Count == 0)
            return;
        if (projectIds.Any(projectId => projectId == Guid.Empty))
            throw new ArgumentException("Project id cannot be empty", nameof(projectIds));

        var idsToDelete = projectIds.ToHashSet();
        var removedCount = _projects.RemoveAll(project => idsToDelete.Contains(project.Id));
        if (removedCount == 0)
            return;

        MarkUpdated();
    }

    public void AddNewProject(string name, string? description, Period period, IReadOnlyCollection<Tag> tags)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be empty", nameof(name));

        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(tags);

        var newProject = new Project(name, description ?? string.Empty, period, this.Id);
        newProject.AddTagRange(tags);
        _projects.Add(newProject);
        MarkUpdated();
    }

    public void RemoveAttributeValue(Attribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (attribute.IsSystem)
            throw new InvalidOperationException("System attribute cannot be removed from a profile");

        var attributeValue = _attributeValues.FirstOrDefault(value => value.AttributeId == attribute.Id);
        if (attributeValue == null)
            return;

        _attributeValues.Remove(attributeValue);
        MarkUpdated();
    }

    public void SetAttributeValue(Guid attributeId, object? value, AttributeType type, int order)
    {
        var attributeValue = _attributeValues.FirstOrDefault(a => a.AttributeId == attributeId);
        if (attributeValue != null)
        {
            attributeValue.UpdateAttributeValue(value, type);
            MarkUpdated();
            return;
        }
        var newAttributeValue = new ProfileAttributeValue(this.Id, attributeId, order);
        newAttributeValue.UpdateAttributeValue(value, type);
        _attributeValues.Add(newAttributeValue);
        MarkUpdated();
    }

    public void RenameProject(Guid projectId, string name)
    {
        if (FindProject(projectId).RenameProject(name))
            MarkUpdated();
    }

    public void SetProjectDescription(Guid projectId, string? description)
    {
        if (FindProject(projectId).SetDescription(description))
            MarkUpdated();
    }

    public void SetProjectPeriod(Guid projectId, Period period)
    {
        if (FindProject(projectId).SetPeriod(period))
            MarkUpdated();
    }

    public void AddProjectTagRange(Guid projectId, IReadOnlyCollection<Tag> tags)
    {
        if (FindProject(projectId).AddTagRange(tags))
            MarkUpdated();
    }

    public void RemoveProjectTagRange(Guid projectId, IReadOnlyCollection<string> tags)
    {
        if (FindProject(projectId).RemoveTagRange(tags))
            MarkUpdated();
    }

    private Project FindProject(Guid projectId)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("Project id cannot be empty", nameof(projectId));

        return _projects.FirstOrDefault(project => project.Id == projectId)
            ?? throw new InvalidOperationException("Project not found");
    }

    private void MarkUpdated()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
