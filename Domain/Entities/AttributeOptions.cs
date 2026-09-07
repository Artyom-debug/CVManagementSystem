using Domain.Abstractions;

namespace Domain.Entities;

public sealed class AttributeOptions : BaseEntity
{
    public string Option { get; private set; }

    public Guid AttributeId { get; private set; }

    public Attribute? Attribute { get; private set; }

    public AttributeOptions(string option, Guid attributeId)
    {
        if(string.IsNullOrWhiteSpace(option))
            throw new ArgumentException("Dropdown option can't be empty", nameof(option));
        if (attributeId == Guid.Empty)
            throw new ArgumentException("Attribute id cannot be empty", nameof(attributeId));

        Option = option.Trim();
        AttributeId = attributeId;
    }

    public void UpdateOption(string newName)
    {
        if(string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Dropdown option can't be empty", nameof(newName));

        var normalizedName = newName.Trim();
        if (Option == normalizedName)
            return;

        Option = normalizedName;
    }
}

