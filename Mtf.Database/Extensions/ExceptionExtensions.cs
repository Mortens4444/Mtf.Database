using Mtf.Database.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Mtf.Database.Extensions;

public static class ExceptionExtensions
{
    public static IEnumerable<LogEntry> ToLogEntries(this Exception exception, string? logger = null, string severity = "Error")
    {
        var list = new List<LogEntry>();
        var current = exception;
        while (current != null)
        {
            list.Add(new LogEntry
            {
                Severity = severity,
                Logger = logger,
                Message = current.Message,
                Exception = current.GetType().FullName,
                StackTrace = current.StackTrace,
                CreatedAt = DateTimeOffset.UtcNow
            });
            current = current.InnerException;
        }
        return list;
    }

    public static string ToFullExceptionString(this Exception exception, string separator = "\n---> ")
    {
        if (exception == null) return string.Empty;

        var sb = new StringBuilder(exception.Message);
        var current = exception.InnerException;

        while (current != null)
        {
            sb.Append(separator).Append(current.Message);
            current = current.InnerException;
        }

        return sb.ToString();
    }
}
