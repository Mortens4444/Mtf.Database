using Microsoft.Extensions.Logging;
using Mtf.Database.Extensions;
using System;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Text.RegularExpressions;

namespace Mtf.Database.Interfaces;

public static partial class ILoggerExtensions
{
    public static void Log(this ILogger logger, Exception exception, string message, params object[] args)
    {
        var msg = FormatMessageTemplate(message, args);
        var exceptionDetails = String.Concat(msg, " - ", exception.ToFullExceptionString());

        LogError(logger, exceptionDetails);
    }

    public static void Log(this ILogger logger, Exception exception)
    {
        var exceptionDetails = exception.ToFullExceptionString();
        LogError(logger, exceptionDetails);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "An error occurred: {Message}")]
    private static partial void LogError(ILogger logger, string message);

    private static readonly Regex PlaceholderRegex = new("{[^{}]+}", RegexOptions.Compiled);

    private static string FormatMessageTemplate(string message, object[] args)
    {
        var index = 0;
        return PlaceholderRegex.Replace(message, match =>
            index < args.Length ? Convert.ToString(args[index++], CultureInfo.InvariantCulture) ?? string.Empty : match.Value);
    }
}