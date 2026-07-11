using Microsoft.Extensions.Logging;
using Mtf.Database.Extensions;
using System;

namespace Mtf.Database.Interfaces;

public static partial class ILoggerExtensions
{
    public static void Log<T>(this ILogger<T> logger, Exception exception)
    {
        var exceptionDetails = exception.ToFullExceptionString();

        LogError(logger, exceptionDetails);

#if DEBUG
        throw new Exception(exceptionDetails, exception);
#else
        throw new Exception(exception.Message, exception);
#endif
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "An error occurred: {Message}")]
    private static partial void LogError(ILogger logger, string message);
}