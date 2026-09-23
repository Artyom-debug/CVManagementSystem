using Domain.Abstractions;

namespace Domain.Value_Objects;

public sealed class Period : ValueObject 
{
    public DateOnly Start { get; }

    public DateOnly? End { get; }

    public Period(DateOnly start, DateOnly? end = null)
    {
        if (end.HasValue && end.Value < start)
            throw new ArgumentException("End date can't be earlier than start date");

        Start = start;
        End = end;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Start;
        yield return End ?? default(DateOnly);
    }

}
