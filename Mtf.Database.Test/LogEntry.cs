namespace Mtf.Database.Test;

public class LogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Severity { get; set; } = "Error";

    public string? Logger { get; set; }

    public string? Message { get; set; }

    public string? Exception { get; set; }

    public string? StackTrace { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
