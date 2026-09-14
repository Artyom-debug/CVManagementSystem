namespace Application.Common.Exceptions;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string entityName, Guid id)
        : base($"{entityName} '{id}' was changed by another request. Reload it and try again.")
    {
    }
}
