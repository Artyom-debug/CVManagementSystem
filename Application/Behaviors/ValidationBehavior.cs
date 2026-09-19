using Application.Common.Models;
using FluentValidation;
using MediatR;

namespace Application.Behaviors;

internal sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return next(cancellationToken);

        return ValidateAndContinueAsync(request, next, cancellationToken);
    }

    private async Task<TResponse> ValidateAndContinueAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(_validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToArray();

        if (failures.Length > 0)
        {
            var message = string.Join("; ", failures.Select(failure => $"{failure.PropertyName}: {failure.ErrorMessage}"));

            if (typeof(TResponse) == typeof(Result))
                return (TResponse)(object)Result.Failure(message);

            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
