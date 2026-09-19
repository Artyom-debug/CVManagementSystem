using Domain.Abstractions;
using Domain.Enums;

namespace Domain.Entities;

public sealed class Attribute : BaseEntity
{
    public string Name { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public AttributeType Type { get; private set; }

    public Category Category { get; private set; }

    public bool IsSystem { get; private set; }

    private readonly List<AttributeOptions> _options = new();

    public IReadOnlyCollection<AttributeOptions> Options => _options.AsReadOnly();

    public Attribute(string name, string? description, AttributeType type, Category category, bool isSystem)
    {
        if(string.IsNullOrWhiteSpace(name)) 
            throw new ArgumentException("Attribute name must be unique and not empty", nameof(name));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown attribute type");
        if (!Enum.IsDefined(category))
            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown attribute category");

        Name = NormalizeName(name.Trim());
        Description = description ?? string.Empty;
        Type = type;
        Category = category;
        IsSystem = isSystem;
    }

    public void RenameAttribute(string newName)
    {
        if (this.IsSystem)
            throw new InvalidOperationException("Can't rename system attribute");
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));
        var name = NormalizeName(newName.Trim());
        if (this.Name == name)
            return;
        this.Name = name;
    }

    public void AddDescription(string newDescription)
    {
        if (this.IsSystem)
            throw new InvalidOperationException("Can't add description for system attribute");
        var description = newDescription ?? string.Empty;
        if (Description == description)
            return;

        Description = description;
    }

    public void MarkAsSystemAttribute()
    {
        if(this.IsSystem)
            throw new InvalidOperationException("This attribute has already marked as system");
        this.IsSystem = true;
    }

    public void ChangeAttributeCategory(Category category)
    {
        if (this.IsSystem)
            throw new InvalidOperationException("Can't change attribute category for system attribute");
        if (!Enum.IsDefined(category))
            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown attribute category");
        if (Category == category)
            return;

        Category = category;
    }

    public void AddOptionsRange(IReadOnlyCollection<string> options)
    {
        if (IsSystem)
            throw new InvalidOperationException("Cannot modify system attribute");
        if (Type != AttributeType.Dropdown)
            throw new InvalidOperationException("This attribute does not support dropdown options");
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count == 0)
            return;
        if (options.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Option values cannot be empty or whitespace", nameof(options));

        var existingValues = _options
            .Select(option => option.Option)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newOptions = options
            .Select(option => option.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(option => !existingValues.Contains(option))
            .Select(option => new AttributeOptions(option, Id))
            .ToList();

        if (newOptions.Count == 0)
            return;

        _options.AddRange(newOptions);
    }

    public void RemoveOptionRange(IReadOnlyCollection<Guid> optionIds)
    {
        if (IsSystem)
            throw new InvalidOperationException("Can't modify system attribute");
        if (Type != AttributeType.Dropdown)
            throw new InvalidOperationException("This attribute doesn't support dropdown options");
        ArgumentNullException.ThrowIfNull(optionIds);
        if (optionIds.Count == 0)
            return;
        if (optionIds.Any(optionId => optionId == Guid.Empty))
            throw new ArgumentException("Option id cannot be empty", nameof(optionIds));

        var idsToRemove = optionIds.ToHashSet();
        var toRemove = _options
            .Where(option => idsToRemove.Contains(option.Id))
            .ToHashSet();
        if (toRemove.Count == 0)
            return;

        _options.RemoveAll(o => toRemove.Contains(o));
    }

    public void RenameOption(Guid optionId, string newName)
    {
        if (IsSystem)
            throw new InvalidOperationException("Can't modify system attribute");
        if (Type != AttributeType.Dropdown)
            throw new InvalidOperationException("This attribute doesn't support dropdown options");
        var option = _options.FirstOrDefault(o => o.Id == optionId);
        if (option == null)
            throw new ArgumentException("Option not found");
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Dropdown option can't be empty", nameof(newName));

        var optionName = newName.Trim();
        if (_options.Any(existing => existing.Id != optionId && string.Equals(existing.Option, optionName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Option '{optionName}' already exists");

        option.UpdateOption(optionName);
    }

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}
