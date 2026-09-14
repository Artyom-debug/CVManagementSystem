using Domain.Abstractions;
using Domain.Value_Objects;

namespace Domain.Entities;

public sealed class Project : BaseEntity
{
    public string Name { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public Period Period { get; private set; }

    public Guid ProfileId { get; private set; }

    public Profile? Profile { get; private set; }

    private readonly List<Tag> _tags = new();

    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

    private Project()
    {
        Name = null!;
        Period = null!;
    }

    public Project(string name, string description, Period period, Guid profileId)
    {
        if(string.IsNullOrWhiteSpace(name)) 
            throw new ArgumentException("Project name can't be empty", nameof(name));
        if (profileId == Guid.Empty)
            throw new ArgumentException("Profile id cannot be empty", nameof(profileId));
        ArgumentNullException.ThrowIfNull(period);

        Name = name;
        Description = description ?? string.Empty;
        Period = period;
        ProfileId = profileId;
    }
    
    internal bool RenameProject(string newName)
    {
        if(string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Project name can't be empty", nameof(newName));
        if (Name == newName)
            return false;

        Name = newName;
        return true;
    }

    internal bool SetDescription(string? newDescription)
    {
        var description = newDescription ?? string.Empty;
        if (Description == description)
            return false;

        Description = description;
        return true;
    }

    internal bool SetPeriod(Period period)
    {
        ArgumentNullException.ThrowIfNull(period);
        if (Period == period)
            return false;

        Period = period;
        return true;
    }

    internal bool AddTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (_tags.Contains(tag))
            return false;

        _tags.Add(tag);
        return true;
    }

    internal bool AddTagRange(IReadOnlyCollection<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Count == 0)
            return false;
        if (tags.Any(tag => tag is null))
            throw new ArgumentException("Tags cannot contain null values", nameof(tags));

        var existingTags = _tags.ToHashSet();
        var tagsToAdd = tags
            .Distinct()
            .Where(tag => !existingTags.Contains(tag))
            .ToList();

        if (tagsToAdd.Count == 0)
            return false;

        _tags.AddRange(tagsToAdd);
        return true;
    }

    internal bool RemoveTag(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag cannot be empty", nameof(name));
        var existingTag = _tags.FirstOrDefault(tag => tag == new Tag(name));
        if (existingTag == null)
            return false;

        _tags.Remove(existingTag);
        return true;
    }

    internal bool RemoveTagRange(IReadOnlyCollection<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count == 0)
            return false;
        if (names.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Tag cannot be empty", nameof(names));

        var tagsToRemove = names
            .Select(name => new Tag(name))
            .ToHashSet();
        var removedCount = _tags.RemoveAll(tagsToRemove.Contains);

        if (removedCount == 0)
            return false;

        return true;
    }
}
