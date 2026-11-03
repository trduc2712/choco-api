using Choco.SharedKernel.Results;

using MediatR;

using Microsoft.Extensions.Logging;

namespace Choco.Application.Abstractions.Behaviors;

internal sealed partial class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly TimeProvider _timeProvider;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        LogProcessing(requestName);

        long startTimestamp = _timeProvider.GetTimestamp();

        TResponse response = await next(cancellationToken);

        double elapsedMilliseconds = _timeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;

        if (response.IsFailure)
        {
            LogFailed(requestName, elapsedMilliseconds, response.Error.Code);
        }
        else
        {
            LogCompleted(requestName, elapsedMilliseconds);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing {RequestName}")]
    private partial void LogProcessing(string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed {RequestName} in {ElapsedMilliseconds:0.0} ms")]
    private partial void LogCompleted(string requestName, double elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Completed {RequestName} in {ElapsedMilliseconds:0.0} ms with error {ErrorCode}")]
    private partial void LogFailed(string requestName, double elapsedMilliseconds, string errorCode);
}
