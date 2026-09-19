using Application.Interfaces;
using MediatR.Pipeline;
using Microsoft.Extensions.Logging;
namespace Application.Behaviors;


public class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger _logger;
    private readonly IUser _user;

    public LoggingBehaviour(ILogger<TRequest> logger, IUser user)
    {
        _logger = logger;
        _user = user;
    }

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _user.Id ?? string.Empty;

        _logger.LogInformation("Project Request: {Name} {UserId}", requestName, userId);

        return Task.CompletedTask;
    }
}
