using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Mtf.Database.Enums;
using System.Net;

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Mtf.Database.Tests;

/// <summary>
/// The library keeps its configuration in static properties, so every test class applies the same
/// settings and the assembly runs without parallelization.
/// </summary>
internal static class TestEnvironment
{
    public const string ScriptsLocation = "Tests.Scripts";

    public static void Configure()
    {
        BaseRepository.DbProvider = DbProviderType.SQLite;
        BaseRepository.DatabaseScriptsAssembly = typeof(TestEnvironment).Assembly;
        BaseRepository.DatabaseScriptsLocation = ScriptsLocation;
        BaseRepository.CommandTimeout = 30;
    }
}

/// <summary>
/// A private shared-cache in-memory SQLite database. It lives as long as this keep-alive connection is open,
/// so every connection the repository opens with <see cref="ConnectionString"/> sees the same data.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection keepAlive;

    public SqliteTestDatabase(bool createRowsTable = true)
    {
        TestEnvironment.Configure();
        ConnectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        keepAlive = new SqliteConnection(ConnectionString);
        keepAlive.Open();
        if (createRowsTable)
        {
            Execute("CREATE TABLE Rows (Id INTEGER PRIMARY KEY, Name TEXT, Notes TEXT, Status TEXT);");
        }
    }

    public string ConnectionString { get; }

    public void Execute(string sql)
    {
        using var command = keepAlive.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public long Count(string table = "Rows")
    {
        using var command = keepAlive.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose() => keepAlive.Dispose();
}

internal sealed class FakeLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception), exception));
    }
}

internal sealed class FakeHttpHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Respond(request));
    }
}
