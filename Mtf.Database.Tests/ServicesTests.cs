using Microsoft.Extensions.Logging;
using Mtf.Database.Exceptions;
using Mtf.Database.Extensions;
using Mtf.Database.Interfaces;
using Mtf.Database.Models;
using Mtf.Database.Services;
using System.Text;
using Xunit;

namespace Mtf.Database.Tests;

public sealed class ServicesTests
{
    public ServicesTests()
    {
        TestEnvironment.Configure();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("Server=x;Database=Sales;", "Sales")]
    [InlineData("Data Source=x;Initial Catalog=Logs;", "Logs")]
    [InlineData("Data Source=file.db", null)]
    public void GetDatabaseName_ReadsEitherKey(string? connectionString, string? expected)
    {
        Assert.Equal(expected, Utils.GetDatabaseName(connectionString));
    }

    [Fact]
    public void ScriptCache_CachesRemovesAndClears()
    {
        ScriptCache.Clear();

        var first = ScriptCache.GetScript("Row.CountRows");
        Assert.Same(first, ScriptCache.GetScript("Row.CountRows"));
        Assert.Contains("COUNT(*)", first, StringComparison.Ordinal);

        Assert.True(ScriptCache.Remove("Row.CountRows"));
        Assert.False(ScriptCache.Remove("Row.CountRows"));
        Assert.NotSame(first, ScriptCache.GetScript("Row.CountRows"));

        ScriptCache.Clear();
        Assert.False(ScriptCache.Remove("Row.CountRows"));
    }

    [Fact]
    public void ScriptCache_MissingScript_Throws()
    {
        Assert.Throws<ArgumentException>(() => ScriptCache.GetScript("Nope.Missing"));
    }

    [Fact]
    public void ResourceHelper_ReadsEmbeddedResources()
    {
        Assert.Contains("INSERT INTO Rows", ResourceHelper.GetDbScript("Row.InsertRow"), StringComparison.Ordinal);
        Assert.Contains("INSERT INTO Rows", ResourceHelper.ReadEmbeddedResource("Tests.Scripts.Row.InsertRow.sql", Encoding.UTF8), StringComparison.Ordinal);
        Assert.Contains("INSERT INTO Rows", ResourceHelper.ReadEmbeddedResource("Tests.Scripts.Row.InsertRow.sql", typeof(ServicesTests).Assembly, Encoding.UTF8), StringComparison.Ordinal);

        using var stream = ResourceHelper.GetEmbeddedResourceStream("Tests.Scripts.Row.InsertRow.sql", typeof(ServicesTests).Assembly);
        Assert.True(stream.Length > 0);
    }

    [Fact]
    public void ResourceHelper_MissingResource_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => ResourceHelper.GetEmbeddedResourceStream("Nope", typeof(ServicesTests).Assembly));
        Assert.Contains("Nope", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => ResourceHelper.GetEmbeddedResourceStream("Nope", null!));
        Assert.Throws<ArgumentException>(() => ResourceHelper.ReadEmbeddedResource("Nope", typeof(ServicesTests).Assembly, Encoding.UTF8));
    }

    [Fact]
    public void ResourceHelper_DefaultsToExecutingAssembly()
    {
        var original = BaseRepository.DatabaseScriptsAssembly;
        try
        {
            BaseRepository.DatabaseScriptsAssembly = null;
            Assert.Throws<ArgumentException>(() => ResourceHelper.GetDbScript("Row.InsertRow"));
        }
        finally
        {
            BaseRepository.DatabaseScriptsAssembly = original;
        }
    }

    [Fact]
    public void ToLogEntries_FlattensTheChain()
    {
        var exception = new InvalidOperationException("outer", new ArgumentException("inner"));

        var entries = exception.ToLogEntries("MyLogger", "Warn").ToList();

        Assert.Equal(2, entries.Count);
        Assert.Equal("outer", entries[0].Message);
        Assert.Equal(typeof(InvalidOperationException).FullName, entries[0].Exception);
        Assert.Equal("MyLogger", entries[0].Logger);
        Assert.Equal("Warn", entries[0].Severity);
        Assert.Equal("inner", entries[1].Message);
        Assert.True(entries[1].CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ToFullExceptionString_JoinsMessages()
    {
        var exception = new InvalidOperationException("outer", new ArgumentException("inner", new Exception("deepest")));

        Assert.Equal("outer\n---> inner\n---> deepest", exception.ToFullExceptionString());
        Assert.Equal("outer | inner | deepest", exception.ToFullExceptionString(" | "));
        Assert.Equal(string.Empty, ((Exception)null!).ToFullExceptionString());
        Assert.Equal("solo", new Exception("solo").ToFullExceptionString());
    }

    [Fact]
    public void LoggerExtensions_LogWithTemplate()
    {
        var logger = new FakeLogger<ServicesTests>();
        var exception = new InvalidOperationException("outer", new Exception("inner"));

        logger.Log(exception, "Failed {Operation} for {Id} and {Missing}", "insert", 7);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("An error occurred: Failed insert for 7 and {Missing} - outer\n---> inner", entry.Message);
    }

    [Fact]
    public void LoggerExtensions_LogExceptionOnly()
    {
        var logger = new FakeLogger<ServicesTests>();

        logger.Log(new InvalidOperationException("boom"));

        Assert.Equal("An error occurred: boom", Assert.Single(logger.Entries).Message);
    }

    [Fact]
    public void SqlScriptExecutionException_Constructors()
    {
        var plain = new SqlScriptExecutionException();
        Assert.Equal(string.Empty, plain.ScriptName);
        Assert.Null(plain.DatabaseName);

        Assert.Equal("msg", new SqlScriptExecutionException("msg").Message);

        var inner = new Exception("x");
        var full = new SqlScriptExecutionException("Db", "Script.Name", inner);
        Assert.Equal("Unable to execute script: Db - Script.Name", full.Message);
        Assert.Equal("Db", full.DatabaseName);
        Assert.Equal("Script.Name", full.ScriptName);
        Assert.Same(inner, full.InnerException);

        full.DatabaseName = "Other";
        full.ScriptName = "Other.Script";
        Assert.Equal("Other", full.DatabaseName);
        Assert.Equal("Other.Script", full.ScriptName);
    }

    [Fact]
    public void Models_HaveDefaults()
    {
        var entry = new LogEntry { Id = 1, Message = "m", StackTrace = "s" };
        Assert.Equal("Error", entry.Severity);
        Assert.Null(entry.Logger);
        Assert.Equal("m", entry.Message);
        Assert.Equal("s", entry.StackTrace);
        Assert.Equal(1, ((IHasIdentifier<long>)entry).Id);

        var param = new SqlParam { ScriptName = "s", Param = 1 };
        Assert.Equal("s", param.ScriptName);
        Assert.Equal(1, param.Param);

        Assert.Equal("INT", TypeMapping.Mappings[typeof(int)]);
        Assert.Equal("BIGINT", TypeMapping.Mappings[typeof(long)]);
    }
}
