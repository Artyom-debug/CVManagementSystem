namespace Application.Common.Models;

public sealed class AuthenticationResult
{
    private AuthenticationResult(bool succeeded, TokenPair? tokens, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Tokens = tokens;
        Errors = errors.ToArray();
    }

    public bool Succeeded { get; }

    public TokenPair? Tokens { get; }

    public string[] Errors { get; }

    public static AuthenticationResult Success(TokenPair tokens) =>
        new(true, tokens, []);

    public static AuthenticationResult Failure(IEnumerable<string> errors) =>
        new(false, null, errors);

    public static AuthenticationResult Failure(string error) =>
        new(false, null, [error]);
}
