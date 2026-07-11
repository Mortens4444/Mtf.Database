using Microsoft.Extensions.Logging;
using Mtf.Database.Extensions;
using System;
using System.Runtime.InteropServices.JavaScript;

namespace Mtf.Database.Interfaces;

public static partial class ILoggerExtensions
{
    public static void Log(this ILogger logger, Exception exception, string message, params object[] args)
    {
        var msg = String.Format(message, args);
        var exceptionDetails = String.Concat(msg, " - ", exception.ToFullExceptionString());

        LogError(logger, exceptionDetails);

#if DEBUG
        throw new Exception(exceptionDetails, exception);
#else
        throw new Exception(exception.Message, exception);
#endif
    }

    public static void Log(this ILogger logger, Exception exception)
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