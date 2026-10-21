using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Domain.Common;

namespace Mundialito.Application.Abstractions.Behaviors;

/// <summary>
/// Structured logging around every request. Only the request type name and the outcome are logged,
/// never the payload, so sensitive data cannot leak into logs.
/// </summary>
internal sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        LogStarted(logger, requestName);

        var response = await next();

        stopwatch.Stop();

        if (response.IsSuccess)
        {
            LogCompleted(logger, requestName, stopwatch.ElapsedMilliseconds);
        }
        else
        {
            LogFailed(logger, requestName, response.Error.Code, response.Error.Type, stopwatch.ElapsedMilliseconds);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling {RequestName}")]
    private static partial void LogStarted(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed {RequestName} in {ElapsedMs} ms")]
    private static partial void LogCompleted(ILogger logger, string requestName, long elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Request {RequestName} failed with {ErrorCode} ({ErrorType}) in {ElapsedMs} ms")]
    private static partial void LogFailed(
        ILogger logger,
        string requestName,
        string errorCode,
        ErrorType errorType,
        long elapsedMs);
}
