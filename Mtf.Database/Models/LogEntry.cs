using Mtf.Database.Interfaces;
using System;

namespace Mtf.Database.Models;

public class LogEntry : IHasIdentifier<long>
{
    public long Id { get; set; }

    public string Severity { get; set; } = "Error";

    public string? Logger { get; set; }

    public string? Message { get; set; }

    public string? Exception { get; set; }

    public string? StackTrace { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
