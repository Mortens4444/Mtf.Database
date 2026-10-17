using Mtf.Database.Enums;
using Mtf.Database.Exceptions;
using Mtf.Database.Models;
using Xunit;

namespace Mtf.Database.Tests;

public sealed class BaseRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase db = new();
    private readonly RowRepository repo;

    public BaseRepositoryTests()
    {
        repo = new RowRepository(db.ConnectionString);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ConnectionString_IsExposed()
    {
        Assert.Equal(db.ConnectionString, repo.ConnectionString);
        Assert.Equal(30, BaseRepository.CommandTimeout);
    }

    [Fact]
    public void Execute_RunsScriptInTransaction()
    {
        repo.Execute("Row.InsertRow", new Row { Id = 1, Name = "a", Status = "s" });

        Assert.Equal(1, db.Count());
    }

    [Fact]
    public void Execute_WithoutParameters_Works()
    {
        db.Execute("INSERT INTO Rows (Id) VALUES (5);");

        repo.Execute("Row.CountRows");
    }

    [Fact]
    public void Execute_FailingScript_IsWrappedAndRolledBack()
    {
        var ex = Assert.Throws<SqlScriptExecutionException>(() => repo.Execute("Row.FailingRow"));

        Assert.Equal("Row.FailingRow", ex.ScriptName);
        Assert.Null(ex.DatabaseName);
        Assert.NotNull(ex.InnerException);
        Assert.StartsWith("Unable to execute script:", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_MissingScript_IsWrapped()
    {
        var ex = Assert.Throws<SqlScriptExecutionException>(() => repo.Execute("Row.NoSuchScript", new Row()));

        Assert.Equal("Row.NoSuchScript", ex.ScriptName);
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void Execute_OverLongValue_ThrowsBeforeAnyWrite()
    {
        var ex = Assert.Throws<ValueTooLongException>(() => repo.Execute("Row.InsertRow", new Row { Id = 1, Name = "0123456789" }));

        Assert.Equal("Name", ex.PropertyName);
        Assert.Equal(0, db.Count());
    }

    [Fact]
    public async Task ExecuteAsync_RunsScript()
    {
        await repo.ExecuteAsync("Row.InsertRow", new Row { Id = 1, Name = "a" });

        Assert.Equal(1, db.Count());
    }

    [Fact]
    public async Task ExecuteAsync_FailingScript_IsWrapped()
    {
        var ex = await Assert.ThrowsAsync<SqlScriptExecutionException>(() => repo.ExecuteAsync("Row.FailingRow"));

        Assert.Equal("Row.FailingRow", ex.ScriptName);
    }

    [Fact]
    public async Task ExecuteAsync_OverLongValue_FaultsTheTask()
    {
        var task = repo.ExecuteAsync("Row.InsertRow", new Row { Id = 1, Name = "0123456789" });

        await Assert.ThrowsAsync<ValueTooLongException>(() => task);
    }

    [Fact]
    public void ExecuteWithoutTransaction_RunsScript()
    {
        repo.ExecuteWithoutTransaction("Row.InsertRow", new Row { Id = 1, Name = "a" });

        Assert.Equal(1, db.Count());
    }

    [Fact]
    public void ExecuteWithoutTransaction_MissingScript_ThrowsRaw()
    {
        Assert.Throws<ArgumentException>(() => repo.ExecuteWithoutTransaction("Row.NoSuchScript"));
    }

    [Fact]
    public void ExecuteWithoutTransaction_OverLongValue_Throws()
    {
        Assert.Throws<ValueTooLongException>(() => repo.ExecuteWithoutTransaction("Row.InsertRow", new Row { Name = "0123456789" }));
    }

    [Fact]
    public void ExecuteParams_RunsAllScriptsInOneTransaction()
    {
        repo.Execute(
            new SqlParam { ScriptName = "Row.InsertRow", Param = new Row { Id = 1, Name = "a" } },
            null!,
            new SqlParam { ScriptName = "Row.InsertRow", Param = new Row { Id = 2, Name = "b" } });

        Assert.Equal(2, db.Count());
    }

    [Fact]
    public void ExecuteParams_EmptyOrNull_DoesNothing()
    {
        repo.Execute();
        repo.Execute((SqlParam[])null!);
        repo.Execute(new SqlParam[] { null! });

        Assert.Equal(0, db.Count());
    }

    [Fact]
    public void ExecuteParams_FailingScript_RollsBackEverything()
    {
        var ex = Assert.Throws<SqlScriptExecutionException>(() => repo.Execute(
            new SqlParam { ScriptName = "Row.InsertRow", Param = new Row { Id = 1, Name = "a" } },
            new SqlParam { ScriptName = "Row.FailingRow" }));

        Assert.Equal("Row.FailingRow", ex.ScriptName);
        Assert.Equal(0, db.Count());
    }

    [Fact]
    public void ExecuteParams_ValidatesEverythingBeforeAnyWrite()
    {
        Assert.Throws<ValueTooLongException>(() => repo.Execute(
            new SqlParam { ScriptName = "Row.InsertRow", Param = new Row { Id = 1, Name = "a" } },
            new SqlParam { ScriptName = "Row.InsertRow", Param = new Row { Id = 2, Name = "0123456789" } }));

        Assert.Equal(0, db.Count());
    }

    [Fact]
    public void ExecuteParams_MissingScript_IsWrapped()
    {
        var ex = Assert.Throws<SqlScriptExecutionException>(() => repo.Execute(new SqlParam { ScriptName = "Row.NoSuchScript" }));

        Assert.Equal("Row.NoSuchScript", ex.ScriptName);
    }

    [Fact]
    public void ExecuteScalarQuery_ReturnsString()
    {
        Assert.Equal("hello", repo.ExecuteScalarQuery("SELECT 'hello'"));
    }

    [Fact]
    public void ExecuteQuery_ReturnsDataTable()
    {
        db.Execute("INSERT INTO Rows (Id, Name) VALUES (1, 'a'), (2, 'b');");

        using var table = repo.ExecuteQuery("SELECT Id, Name FROM Rows WHERE Id = @Id", new Dictionary<string, object> { ["@Id"] = 2L });

        Assert.Single(table.Rows);
        Assert.Equal("b", table.Rows[0]["Name"]);
    }

    [Fact]
    public void ExecuteQuery_NullParameterValue_IsSentAsDbNull()
    {
        using var table = repo.ExecuteQuery("SELECT @Value AS Value", new Dictionary<string, object> { ["@Value"] = null! });

        Assert.Equal(DBNull.Value, table.Rows[0]["Value"]);
    }

    [Fact]
    public void ExecuteQuery_WithoutParameters_Works()
    {
        using var table = repo.ExecuteQuery("SELECT 1 AS One");

        Assert.Equal(1L, table.Rows[0]["One"]);
    }

    [Fact]
    public void ExecuteQuery_UnsafeQuery_Throws()
    {
        Assert.Throws<ArgumentException>(() => repo.ExecuteQuery("DELETE FROM Rows"));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("SELECT * FROM Rows", true)]
    [InlineData("select * from Rows where Name = 'a'", true)]
    [InlineData("DROP TABLE Rows", false)]
    [InlineData("SELECT 1; SELECT 2", false)]
    [InlineData("SELECT 1 -- comment", false)]
    [InlineData("SELECT /* x */ 1", false)]
    [InlineData("EXEC xp_cmdshell", false)]
    [InlineData("SELECT 'unbalanced", false)]
    [InlineData("SELECT \"unbalanced", false)]
    public void IsQuerySeemsSafe_DetectsDangerousInput(string? query, bool expected)
    {
        Assert.Equal(expected, BaseRepository.IsQuerySeemsSafe(query!));
    }

    [Theory]
    [InlineData("SELECT * FROM Rows WHERE Id = @Id", false)]
    [InlineData("SELECT * FROM Rows WHERE Id IN @Ids", false)]
    [InlineData("DECLARE @Id INT; SELECT * FROM Rows WHERE Id = @Id AND Name = @Name", false)]
    [InlineData("SELECT 1\nGO\nSELECT 2", true)]
    [InlineData("CREATE PROCEDURE p AS SELECT 1", false)]
    [InlineData("ALTER PROC p AS SELECT @x", true)]
    public void HasValidSqlSyntax_OnSqlite_ReportsTheParseOnlyFailure(string sql, bool checkDeclarations)
    {
        // SET PARSEONLY is SQL Server only, so every batch fails here; the failure must be reported, never thrown.
        var valid = repo.HasValidSqlSyntax(sql, checkDeclarations, out var exception);

        Assert.False(valid);
        Assert.NotNull(exception);
    }

    [Fact]
    public void HasValidSyntax_LoadsTheScript()
    {
        Assert.False(repo.HasValidSyntax("Row.SelectRow", false, out var exception));
        Assert.NotNull(exception);
    }

    [Fact]
    public void ExecuteMigrations_RunsEveryScript()
    {
        using var migrationDb = new SqliteTestDatabase(createRowsTable: false);
        BaseRepository.ScriptsToExecute.Clear();
        BaseRepository.ScriptsToExecute.Add("Migration.CreateRows");

        BaseRepository.ExecuteMigrations(migrationDb.ConnectionString);

        Assert.Equal(0, migrationDb.Count());
    }

    [Fact]
    public void ExecuteMigrations_BrokenScript_IsWrappedWithItsName()
    {
        using var migrationDb = new SqliteTestDatabase(createRowsTable: false);
        BaseRepository.ScriptsToExecute.Clear();
        BaseRepository.ScriptsToExecute.Add("Migration.CreateRows");
        BaseRepository.ScriptsToExecute.Add("Migration.Broken");

        var ex = Assert.Throws<SqlScriptExecutionException>(() => BaseRepository.ExecuteMigrations(migrationDb.ConnectionString));

        Assert.Equal("Migration.Broken", ex.ScriptName);
        BaseRepository.ScriptsToExecute.Clear();
    }

    [Fact]
    public void CreateConnection_UnsupportedProvider_Throws()
    {
        var original = BaseRepository.DbProvider;
        try
        {
            BaseRepository.DbProvider = (DbProviderType)99;
            Assert.Throws<NotSupportedException>(() => repo.OpenConnection());
        }
        finally
        {
            BaseRepository.DbProvider = original;
        }
    }

    [Fact]
    public void CreateConnection_SqlServerProvider_CreatesSqlConnection()
    {
        var original = BaseRepository.DbProvider;
        try
        {
            BaseRepository.DbProvider = DbProviderType.SqlServer;
            using var connection = new RowRepository("Data Source=localhost;Initial Catalog=x;Integrated Security=True").OpenConnection();
            Assert.IsType<Microsoft.Data.SqlClient.SqlConnection>(connection);
        }
        finally
        {
            BaseRepository.DbProvider = original;
        }
    }

    [Fact]
    public void LoadScript_MissingScript_IsWrapped()
    {
        var ex = Assert.Throws<SqlScriptExecutionException>(() => repo.Load("Row.NoSuchScript"));

        Assert.Equal("Row.NoSuchScript", ex.ScriptName);
        Assert.Contains("INSERT INTO Rows", repo.Load("Row.InsertRow"), StringComparison.Ordinal);
    }
}
