using Microsoft.Extensions.Logging;
using Mtf.Database.Attributes;
using Mtf.Database.Interfaces;
using Mtf.Database.Services;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;

namespace Mtf.Database.Tests;

public class Row : IHasIdentifier<long>
{
    public long Id { get; set; }

    [Length(5)]
    public string? Name { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(5)]
    public string? Notes { get; set; }

    public string? Status { get; set; }
}

public sealed class RowRepository : BaseRepository<Row, long>
{
    public RowRepository(string connectionString) : base(connectionString)
    {
    }

    public RowRepository(ILogger<BaseRepository<Row, long>> logger, string connectionString) : base(logger, connectionString)
    {
    }

    public long Count() => ExecuteScalar<long>("Row.CountRows");

    public string? NameOf(long id) => ExecuteScalar<string>("Row.NameOfRow", new { Id = id });

    public int InsertViaScalar(Row row) => ExecuteScalar<int>("Row.InsertRow", row);

    public void RunNullFuncOperation() => ExecuteInTransaction((Func<DbConnection, IDbTransaction, Row>)null!);

    public void RunNullActionOperation() => ExecuteInTransaction((Action<DbConnection, IDbTransaction>)null!);

    public Task RunNullAsyncOperation() => ExecuteInTransactionAsync((Func<DbConnection, DbTransaction, Task<int>>)null!);

    public ReadOnlyCollection<Row> CallStoredProcedure(object? param) => ExecuteStoredProcedure("sp_rows", param);

    public void CallStoredProcedureNonQuery(object? param) => ExecuteStoredProcedureNonQuery("sp_rows", param);

    public Row InsertTwoInTransaction(Row first, Row second, bool failAfterFirst)
    {
        return ExecuteInTransaction((connection, transaction) =>
        {
            ExecuteInternal(connection, ScriptCache.GetScript("Row.InsertRow"), first, transaction);
            if (failAfterFirst)
            {
                throw new InvalidOperationException("boom");
            }
            ExecuteInternal(connection, ScriptCache.GetScript("Row.InsertRow"), second, transaction);
            return second;
        });
    }

    public void InsertInTransaction(Row row, bool fail)
    {
        ExecuteInTransaction((connection, transaction) =>
        {
            ExecuteInternal(connection, ScriptCache.GetScript("Row.InsertRow"), row, transaction);
            if (fail)
            {
                throw new InvalidOperationException("boom");
            }
        });
    }

    public Task<int> InsertInTransactionAsync(Row row, bool fail)
    {
        return ExecuteInTransactionAsync(async (connection, transaction) =>
        {
            var affected = await ExecuteInternalAsync(connection, ScriptCache.GetScript("Row.InsertRow"), row, transaction);
            if (fail)
            {
                throw new InvalidOperationException("boom");
            }
            return affected;
        });
    }

    public static void Validate(string sql, object? param, CommandType commandType = CommandType.Text) => ValidateParameters(sql, param, commandType);

    public string Load(string scriptName) => LoadScript(scriptName);

    public DbConnection OpenConnection() => CreateConnection();
}
