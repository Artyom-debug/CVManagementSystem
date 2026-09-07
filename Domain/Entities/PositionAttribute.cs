using Domain.Abstractions;

namespace Domain.Entities;

public sealed class PositionAttribute : BaseEntity
{
    public Guid AttributeId { get; private set; }

    public Guid PositionId { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsRequired { get; private set; }

    public Position? Position { get; private set; }

    public Attribute? Attribute { get; private set; }

    public PositionAttribute(Guid positionId, Guid attributeId, int displayOrder, bool isRequired)
    {
        if (positionId == Guid.Empty)
            throw new ArgumentException("Position id cannot be empty", nameof(positionId));
        if (attributeId == Guid.Empty)
            throw new ArgumentException("Attribute id cannot be empty", nameof(attributeId));
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), displayOrder, "Display order cannot be negative");
        PositionId = positionId;
        AttributeId = attributeId;
        DisplayOrder = displayOrder;
        IsRequired = isRequired;
    }

    public void ChangeDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), displayOrder, "Display order cannot be negative");
        if (DisplayOrder == displayOrder)
            return;
        this.DisplayOrder = displayOrder;
    }

    public void SetRequired(bool isRequired)
    {
        if (IsRequired == isRequired)
            return;
        IsRequired = isRequired;
    }

    public void UpdateDisplaySettings(int displayOrder, bool isRequired)
    {
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), displayOrder, "Display order cannot be negative");
        if (DisplayOrder == displayOrder && IsRequired == isRequired)
            return;
        DisplayOrder = displayOrder;
        IsRequired = isRequired;
    }
}
