using Domain.Abstractions;
using Domain.Enums;
using Domain.Value_Objects;

namespace Domain.Entities;

public sealed class Position : BaseEntity
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public int MaxProjectCount { get; private set; }
    public bool IsPublic { get; private set; }

    private readonly List<Tag> _tags = new();
    private readonly List<PositionAttribute> _positionAttributes = new();
    private readonly List<AccessRule> _accessRules = new();
    private readonly List<CV> _cvs = new();
    private readonly List<DiscussionPost> _discussionPosts = new();

    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();
    public IReadOnlyCollection<PositionAttribute> PositionAttributes => _positionAttributes.AsReadOnly();
    public IReadOnlyCollection<AccessRule> AccessRules => _accessRules.AsReadOnly();
    public IReadOnlyCollection<CV> CVs => _cvs.AsReadOnly();
    public IReadOnlyCollection<DiscussionPost> DiscussionPosts => _discussionPosts.AsReadOnly();

    public Position(string name, string description, int maxProjectCount) 
    {
        if (string.IsNullOrWhiteSpace(name)) 
            throw new ArgumentException("Name cannot be empty", nameof(name));
        if (maxProjectCount < 0) 
            throw new ArgumentException("MaxProjectCount cannot be negative", nameof(maxProjectCount));
        Name = name;
        Description = description ?? string.Empty;
        MaxProjectCount = maxProjectCount;
        IsPublic = true;
    }

    public void RenamePosition(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));
        if (Name == newName)
            return;
        Name = newName;
    }

    public void AddDescription(string newDescription)
    {
        ArgumentNullException.ThrowIfNull(newDescription);
        if (Description == newDescription)
            return;
        Description = newDescription;
    }

    public void RemoveDescription()
    {
        if (string.IsNullOrEmpty(Description))
            return;
        Description = string.Empty;
    }

    public void SetProjectCount(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "Project count cannot be negative");
        if (MaxProjectCount == count)
            return;
        MaxProjectCount = count;
    }

    public void MakePublicAccess()
    {
        if (this.IsPublic)
            throw new InvalidOperationException("Position has public access");
        this.IsPublic = true;
        _accessRules.Clear();
    }

    public void AddDiscussionPost(string authorId, string content)
    {
        _discussionPosts.Add(new DiscussionPost(Id, authorId, content));
    }

    public void AddAccessRule(AccessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (_accessRules.Contains(rule))
            throw new InvalidOperationException("Position already contains this access rule");

        _accessRules.Add(rule);
        IsPublic = false;
    }

    public void AddAccessRuleRange(IReadOnlyCollection<AccessRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Count == 0)
            throw new ArgumentException("Access rules collection cannot be empty", nameof(rules));

        ValidateAccessRules(rules);

        var newRules = rules.ToHashSet();
        if (newRules.Count != rules.Count)
            throw new ArgumentException("Access rules collection contains duplicates", nameof(rules));
        if (_accessRules.Any(newRules.Contains))
            throw new InvalidOperationException("Position already contains one or more selected access rules");
        _accessRules.AddRange(rules);
        IsPublic = false;
    }

    public void UpdateAccessRule(AccessRule currentRule, AccessRule newRule)
    {
        ArgumentNullException.ThrowIfNull(currentRule);
        ArgumentNullException.ThrowIfNull(newRule);

        var ruleIndex = _accessRules.IndexOf(currentRule);
        if (ruleIndex < 0)
            throw new InvalidOperationException("Access rule not found");
        if (currentRule == newRule)
            return;
        if (_accessRules.Contains(newRule))
            throw new InvalidOperationException("Position already contains this access rule");

        _accessRules[ruleIndex] = newRule;
    }

    public void RemoveAccessRule(AccessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!_accessRules.Remove(rule))
            throw new InvalidOperationException("Access rule not found");
        if (_accessRules.Count == 0)
            IsPublic = true;
    }

    public void RemoveAccessRuleRange(IReadOnlyCollection<AccessRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Count == 0)
            throw new ArgumentException("Access rules collection cannot be empty", nameof(rules));

        ValidateAccessRules(rules);

        var rulesToRemove = rules.ToHashSet();
        if (rulesToRemove.Count != rules.Count)
            throw new ArgumentException("Access rules collection contains duplicates", nameof(rules));
        if (rulesToRemove.Any(rule => !_accessRules.Contains(rule)))
            throw new InvalidOperationException("One or more access rules were not found");
        _accessRules.RemoveAll(rulesToRemove.Contains);
        if (_accessRules.Count == 0)
            IsPublic = true;
    }

    public void AddPositionAttribute(Guid attributeId, int displayOrder)
    {
        if (attributeId == Guid.Empty)
            throw new ArgumentException("Attribute id cannot be empty", nameof(attributeId));
        if (_positionAttributes.Any(item => item.AttributeId == attributeId))
            throw new InvalidOperationException("Position already contains this attribute");
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(
                nameof(displayOrder),
                displayOrder,
                "Display order cannot be negative");
        if (_positionAttributes.Any(item => item.DisplayOrder == displayOrder))
            throw new InvalidOperationException("Position already contains an attribute with this display order");
        var positionAttribute = new PositionAttribute(this.Id, attributeId, displayOrder);
        _positionAttributes.Add(positionAttribute);
    }

    public void AddPositionAttributeRange(IReadOnlyCollection<PositionAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        if (attributes.Count == 0)
            throw new ArgumentException("Position attributes collection cannot be empty", nameof(attributes));
        if (attributes.Any(item => item.DisplayOrder < 0))
            throw new ArgumentException("Display order cannot be negative", nameof(attributes));

        var attributeIds = attributes
            .Select(item => item.AttributeId)
            .ToHashSet();
        if (_positionAttributes.Any(item => attributeIds.Contains(item.AttributeId)))
            throw new InvalidOperationException("Position already contains one or more selected attributes");
        var displayOrders = attributes
            .Select(item => item.DisplayOrder)
            .ToHashSet();
        if (displayOrders.Count != attributes.Count)
            throw new ArgumentException("Display orders must be unique within the collection", nameof(attributes));
        if (_positionAttributes.Any(item => displayOrders.Contains(item.DisplayOrder)))
            throw new InvalidOperationException("Position already contains one or more selected display orders");

        _positionAttributes.AddRange(attributes);
    }

    public void RemovePositionAttribute(Guid attributeId)
    {
        var positionAttribute = FindPositionAttribute(attributeId);
        _positionAttributes.Remove(positionAttribute);
    }

    public void RemovePositionAttributeRange(IReadOnlyCollection<Guid> attributeIds)
    {
        ArgumentNullException.ThrowIfNull(attributeIds);

        if (attributeIds.Count == 0)
            throw new ArgumentException("Attribute ids collection cannot be empty", nameof(attributeIds));
        if (attributeIds.Any(attributeId => attributeId == Guid.Empty))
            throw new ArgumentException("Attribute id cannot be empty", nameof(attributeIds));

        var idsToRemove = attributeIds.ToHashSet();
        if (idsToRemove.Count != attributeIds.Count)
            throw new ArgumentException("Attribute ids collection contains duplicates", nameof(attributeIds));
        if (idsToRemove.Any(attributeId => _positionAttributes.All(item => item.AttributeId != attributeId)))
            throw new InvalidOperationException("One or more position attributes were not found");
        _positionAttributes.RemoveAll(item => idsToRemove.Contains(item.AttributeId));
    }

    public void UpdatePositionAttributeOrders(IReadOnlyDictionary<Guid, int> displayOrders)
    {
        ArgumentNullException.ThrowIfNull(displayOrders);
        if (displayOrders.Count != _positionAttributes.Count || _positionAttributes.Any(item => !displayOrders.ContainsKey(item.AttributeId)))
            throw new ArgumentException("Display orders must be provided for every position attribute", nameof(displayOrders));
        if (displayOrders.Values.Any(displayOrder => displayOrder < 0))
            throw new ArgumentException("Display order cannot be negative", nameof(displayOrders));
        if (displayOrders.Values.Distinct().Count() != displayOrders.Count)
            throw new ArgumentException("Display orders must be unique within a position", nameof(displayOrders));
        var orderChanged = _positionAttributes.Any(item => item.DisplayOrder != displayOrders[item.AttributeId]);
        if (!orderChanged)
            return;
        foreach (var positionAttribute in _positionAttributes)
        {
            positionAttribute.ChangeDisplayOrder(displayOrders[positionAttribute.AttributeId]);
        }
    }

    public void AddTag(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        if (_tags.Contains(tag))
            throw new InvalidOperationException("Position already contains this tag");
        _tags.Add(tag);
    }

    public void AddTagRange(IReadOnlyCollection<Tag> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Count == 0)
            throw new ArgumentException("Position tags collection cannot be empty", nameof(tags));
        if (tags.Any(tag => tag is null))
            throw new ArgumentException("Tags cannot contain null values", nameof(tags));

        var newTags = tags.ToHashSet();
        if (newTags.Count != tags.Count)
            throw new ArgumentException("Position tags collection contains duplicates", nameof(tags));
        if (_tags.Any(newTags.Contains))
            throw new InvalidOperationException("Position already contains one or more selected tags");
        _tags.AddRange(tags);
    }

    public void RemoveTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be empty", nameof(tag));
        var existTag = _tags.FirstOrDefault(existingTag => existingTag == new Tag(tag));
        if (existTag == null)
            throw new InvalidOperationException("Position doesn't contains this tag");
        _tags.Remove(existTag);
    }

    public void RemoveTagRange(IReadOnlyCollection<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count == 0)
            throw new ArgumentException("Project tags collection cannot be empty", nameof(names));

        ValidateTagNames(names);

        var tagsToRemove = names.Select(name => new Tag(name)).ToHashSet();
        if (tagsToRemove.Any(tag => !_tags.Contains(tag)))
            throw new InvalidOperationException("One or more project tags were not found");
        _tags.RemoveAll(tagsToRemove.Contains);
    }

    private static void ValidateAccessRules(IReadOnlyCollection<AccessRule> rules)
    {
        if (rules.Any(rule => rule is null))
            throw new ArgumentException("Access rules collection cannot contain null values");
        if (rules.Any(rule => rule.AttributeId == Guid.Empty))
            throw new ArgumentException("Access rule attribute id cannot be empty");
        if (rules.Any(rule => !Enum.IsDefined(rule.AttributeType)))
            throw new ArgumentException("Access rules collection contains an unknown attribute type");
        if (rules.Any(rule => !Enum.IsDefined(rule.Operator)))
            throw new ArgumentException("Access rules collection contains an unknown operator");
    }

    private static void ValidateTagNames(IReadOnlyCollection<string> tagNames)
    {
        ArgumentNullException.ThrowIfNull(tagNames);

        if (tagNames.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Project tag cannot be empty", nameof(tagNames));
        if (tagNames.Distinct().Count() != tagNames.Count)
            throw new ArgumentException("Project tags collection contains duplicates", nameof(tagNames));
    }

    private PositionAttribute FindPositionAttribute(Guid attributeId)
    {
        if (attributeId == Guid.Empty)
            throw new ArgumentException("Attribute id cannot be empty", nameof(attributeId));
        return _positionAttributes.FirstOrDefault(item => item.AttributeId == attributeId)
            ?? throw new InvalidOperationException("Position attribute not found");
    }
}
