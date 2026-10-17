using Dapper;
using Microsoft.Extensions.Logging;
using Mtf.Database.Exceptions;
using Xunit;

namespace Mtf.Database.Tests;

public sealed class GenericRepositoryTests : IDisposable
{
    private readonly SqliteTestDatabase db = new();
    private readonly FakeLogger<BaseRepository<Row, long>> logger = new();
    private readonly RowRepository repo;

    public GenericRepositoryTests()
    {
        repo = new RowRepository(logger, db.ConnectionString);
    }

    public void Dispose() => db.Dispose();

    private void Seed() => db.Execute("INSERT INTO Rows (Id, Name, Notes, Status) VALUES (1, 'a', 'n', 'open'), (2, 'b', NULL, 'closed'), (3, 'c', NULL, 'open');");

    [Fact]
    public void ScriptsSubfolderName_IsTheEntityName()
    {
        Assert.Equal("Row", repo.ScriptsSubfolderName);
    }

    [Fact]
    public void Insert_Select_Update_Delete_RoundTrip()
    {
        repo.Insert(new Row { Id = 1, Name = "a", Notes = "n", Status = "open" });
        var loaded = repo.Select(1);
        Assert.NotNull(loaded);
        Assert.Equal("a", loaded.Name);

        repo.Update(new Row { Id = 1, Name = "b", Notes = "m", Status = "closed" });
        Assert.Equal("b", repo.Select(1)!.Name);

        repo.Delete(1);
        Assert.Null(repo.Select(1));
    }

    [Fact]
    public void Insert_OverLongValue_ThrowsValueTooLong()
    {
        var ex = Assert.Throws<ValueTooLongException>(() => repo.Insert(new Row { Id = 1, Name = "0123456789" }));

        Assert.Equal("Row", ex.TypeName);
        Assert.Equal(5, ex.MaxLength);
        Assert.Equal(10, ex.ActualLength);
        Assert.Equal(0, db.Count());
    }

    [Fact]
    public void Update_DoesNotValidatePropertiesTheScriptIgnores()
    {
        Seed();

        repo.Execute("Row.UpdateStatusRow", new Row { Id = 1, Name = "0123456789", Notes = "0123456789", Status = "done" });

        Assert.Equal("done", repo.Select(1)!.Status);
    }

    [Fact]
    public void SelectAll_And_SelectWhere()
    {
        Seed();

        Assert.Equal(3, repo.SelectAll().Count);
        var open = repo.SelectWhere(new { Status = "open" });
        Assert.Equal(new long[] { 1, 3 }, open.Select(row => row.Id));
    }

    [Fact]
    public void DeleteWhere_AcceptsObjectsAndDictionaries()
    {
        Seed();

        repo.DeleteWhere(new { Status = "open" });
        Assert.Equal(1, db.Count());

        repo.DeleteWhere(new Dictionary<string, object> { ["Status"] = "closed" });
        Assert.Equal(0, db.Count());
    }

    [Fact]
    public void Execute_WithCollection_RunsOncePerElement()
    {
        repo.Execute("Row.InsertRow", new List<Row> { new() { Id = 1, Name = "a" }, new() { Id = 2, Name = "b" } });

        Assert.Equal(2, db.Count());
    }

    [Fact]
    public void Execute_WithDynamicParameters_PassesThrough()
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", 7);
        parameters.Add("Name", "x");
        parameters.Add("Notes", null);
        parameters.Add("Status", "s");

        repo.Execute("Row.InsertRow", parameters);

        Assert.Equal("x", repo.Select(7)!.Name);
    }

    [Fact]
    public void InsertAndReturnId_ValidatesBeforeRunning()
    {
        Assert.Throws<ValueTooLongException>(() => repo.InsertAndReturnId<long>(new Row { Id = 1, Name = "0123456789" }));
        Assert.Equal(0, db.Count());
    }

    [Fact]
    public void InsertAndReturnId_FailureIsWrappedAndRolledBack()
    {
        // SCOPE_IDENTITY() is SQL Server only, so SQLite rejects the appended SELECT after the insert ran.
        var ex = Assert.Throws<SqlScriptExecutionException>(() => repo.InsertAndReturnId<long>(new Row { Id = 1, Name = "a" }));

        Assert.Equal("Row.InsertRow", ex.ScriptName);
        Assert.Equal(0, db.Count());
    }

    [Fact]
    public void InsertAndReturnId_UnmappedType_UsesTypeName()
    {
        var ex = Assert.Throws<SqlScriptExecutionException>(() => repo.InsertAndReturnId<Guid>(new Row { Id = 1, Name = "a" }));

        Assert.Equal("Row.InsertRow", ex.ScriptName);
    }

    [Fact]
    public void ExecuteScalar_ReturnsValues()
    {
        Seed();

        Assert.Equal(3, repo.Count());
        Assert.Equal("b", repo.NameOf(2));
        Assert.Null(repo.NameOf(99));
    }

    [Fact]
    public void ExecuteScalar_Failure_IsWrapped()
    {
        using var empty = new SqliteTestDatabase(createRowsTable: false);
        var ex = Assert.Throws<SqlScriptExecutionException>(() => new RowRepository(empty.ConnectionString).Count());

        Assert.Equal("Row.CountRows", ex.ScriptName);
    }

    [Fact]
    public void ExecuteScalar_OverLongValue_Throws()
    {
        Assert.Throws<ValueTooLongException>(() => repo.InsertViaScalar(new Row { Id = 1, Name = "0123456789" }));
        Assert.Equal(0, db.Count());
    }

    [Fact]
    public void StoredProcedures_ValidateParameters()
    {
        // Stored procedures bind every property, so a property no SQL references is still validated.
        Assert.Throws<ValueTooLongException>(() => repo.CallStoredProcedure(new Row { Notes = "0123456789" }));
        Assert.Throws<ValueTooLongException>(() => repo.CallStoredProcedureNonQuery(new Row { Notes = "0123456789" }));
    }

    [Fact]
    public void StoredProcedures_OnSqlite_FailLoudly()
    {
        Assert.ThrowsAny<Exception>(() => repo.CallStoredProcedure(new { Id = 1 }));
        var ex = Assert.Throws<SqlScriptExecutionException>(() => repo.CallStoredProcedureNonQuery(new { Id = 1 }));
        Assert.Equal("sp_rows", ex.ScriptName);
    }

    [Fact]
    public void Query_Variants()
    {
        Seed();

        Assert.Equal(3, repo.Query("Row.SelectAllRow").Count);
        Assert.Single(repo.Query("Row.SelectWhereRow", new { Status = "closed" }));
        Assert.Equal("a", repo.QuerySingleOrDefault("Row.SelectRow", 1)!.Name);
        Assert.Equal("b", repo.QuerySingleOrDefault("Row.SelectRow", new { Id = 2 })!.Name);
        Assert.Null(repo.QuerySingleOrDefault("Row.SelectRow", 42));

        dynamic? row = repo.QuerySingleOrDefaultWithDynamic("Row.SelectRow", new { Id = 3 });
        Assert.NotNull(row);
        Assert.Equal("c", (string)row!.Name);
        Assert.Null(repo.QuerySingleOrDefaultWithDynamic("Row.SelectRow", new { Id = 42 }));
    }

    [Fact]
    public async Task QueryAsync_Variants()
    {
        Seed();

        Assert.Equal(3, (await repo.QueryAsync("Row.SelectAllRow")).Count);
        Assert.Equal(2, (await repo.QueryAsync("Row.SelectWhereRow", new { Status = "open" })).Count);
        Assert.Equal(3, (await repo.GetAllAsync()).Count);
        Assert.Single(await repo.GetAllWhereAsync(new { Status = "closed" }));
        Assert.Equal("a", (await repo.GetByIdAsync(1))!.Name);
        Assert.Null(await repo.GetByIdAsync(42));
    }

    [Fact]
    public async Task InsertAsync_UpdateAsync_DeleteAsync_RoundTrip()
    {
        var inserted = await repo.InsertAsync(new Row { Id = 1, Name = "a", Status = "s" });
        Assert.NotNull(inserted);
        Assert.Equal("a", inserted.Name);

        var updated = await repo.UpdateAsync(new Row { Id = 1, Name = "b", Status = "t" });
        Assert.Equal("b", updated!.Name);

        await repo.DeleteAsync(1);
        Assert.Null(await repo.GetByIdAsync(1));
    }

    [Fact]
    public async Task InsertAsync_Failure_IsLoggedAndRethrown()
    {
        var ex = await Assert.ThrowsAsync<ValueTooLongException>(() => repo.InsertAsync(new Row { Id = 1, Name = "0123456789" }));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(ex.Message, entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_Failure_IsLoggedAndRethrown()
    {
        await Assert.ThrowsAsync<ValueTooLongException>(() => repo.UpdateAsync(new Row { Id = 1, Notes = "0123456789" }));

        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task InsertAsync_WithoutLogger_StillThrows()
    {
        var silent = new RowRepository(db.ConnectionString);

        await Assert.ThrowsAsync<ValueTooLongException>(() => silent.InsertAsync(new Row { Id = 1, Name = "0123456789" }));
        await Assert.ThrowsAsync<ValueTooLongException>(() => silent.UpdateAsync(new Row { Id = 1, Name = "0123456789" }));
    }

    [Fact]
    public void ExecuteInTransaction_CommitsOnSuccess()
    {
        var result = repo.InsertTwoInTransaction(new Row { Id = 1, Name = "a" }, new Row { Id = 2, Name = "b" }, failAfterFirst: false);

        Assert.Equal(2, result.Id);
        Assert.Equal(2, db.Count());
    }

    [Fact]
    public void ExecuteInTransaction_RollsBackOnFailure()
    {
        Assert.Throws<InvalidOperationException>(() => repo.InsertTwoInTransaction(new Row { Id = 1, Name = "a" }, new Row { Id = 2, Name = "b" }, failAfterFirst: true));
        Assert.Equal(0, db.Count());

        repo.InsertInTransaction(new Row { Id = 3, Name = "c" }, fail: false);
        Assert.Equal(1, db.Count());

        Assert.Throws<InvalidOperationException>(() => repo.InsertInTransaction(new Row { Id = 4, Name = "d" }, fail: true));
        Assert.Equal(1, db.Count());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsAndRollsBack()
    {
        Assert.Equal(1, await repo.InsertInTransactionAsync(new Row { Id = 1, Name = "a" }, fail: false));
        Assert.Equal(1, db.Count());

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.InsertInTransactionAsync(new Row { Id = 2, Name = "b" }, fail: true));
        Assert.Equal(1, db.Count());
    }

    [Fact]
    public async Task ExecuteInTransaction_NullOperation_Throws()
    {
        Assert.Throws<ArgumentNullException>(repo.RunNullFuncOperation);
        Assert.Throws<ArgumentNullException>(repo.RunNullActionOperation);
        await Assert.ThrowsAsync<ArgumentNullException>(repo.RunNullAsyncOperation);
    }
}
