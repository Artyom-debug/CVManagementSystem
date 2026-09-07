using Domain.Abstractions;

namespace Domain.Value_Objects;

public sealed class Tag : ValueObject
{
    public string Name { get; }

    public Tag(string name)
    {
        if(string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name can't be empty", nameof(name));

        Name = name.Trim();
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Name.ToUpperInvariant();
    }
}
