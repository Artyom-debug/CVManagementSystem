namespace Application.Common.Models;

public sealed class Result
{
    internal Result(bool succeeded, IEnumerable<string> errors, int? version)
    {
        Succeeded = succeeded;
        Errors = errors.ToArray();
        Version = version;
    }

    public bool Succeeded { get; init; }

    public string[] Errors { get; init; }

    public int? Version { get; init; }

    public static Result Success(int? version = null) => new(true, [], version);

    public static Result Failure(IEnumerable<string> errors) => new(false, errors, null);

    public static Result Failure(string error) => new(false, [error], null);
}
