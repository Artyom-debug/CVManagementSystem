using Domain.Abstractions;
using System.Xml.Linq;

namespace Domain.Value_Objects;

public sealed class Period : ValueObject 
{
    public DateOnly Start { get; }

    public DateOnly? End { get; }

    public Period(DateOnly start, DateOnly end)
    {
        if (end < start)
            throw new ArgumentException("End date can't be earlier than start date");
        this.Start = start;
        this.End = end;
    }

    public Period(DateOnly start) 
    {
        this.Start = start;
        this.End = null;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Start;
        yield return End ?? default(DateOnly);
    }

}
