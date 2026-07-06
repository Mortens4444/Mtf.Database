using Dapper;
using Mtf.Database.Exceptions;
using Mtf.Database.Interfaces;
using Mtf.Database.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Mtf.Database;

public abstract class BaseRepositoryWithCompositeKey<TModelType, TKey>(string connectionString) : BaseRepository(connectionString), IRepositoryWithCompositeKey<TModelType, TKey>
{
    //private static readonly string SelectScriptName = $"{nameof(Select)}{typeof(TModelType).Name}";
    private static readonly string SelectAllScriptName = $"{nameof(SelectAll)}{typeof(TModelType).Name}";
    private static readonly string SelectWhereScriptName = $"{nameof(SelectWhere)}{typeof(TModelType).Name}";
    private static readonly string InsertScriptName = $"{nameof(Insert)}{typeof(TModelType).Name}";
    private static readonly string UpdateScriptName = $"{nameof(Update)}{typeof(TModelType).Name}";
    //private static readonly string DeleteScriptName = $"{nameof(Delete)}{typeof(TModelType).Name}";
    private static readonly string DeleteWhereScriptName = $"{nameof(DeleteWhere)}{typeof(TModelType).Name}";

    protected TModelType ExecuteInTransaction(Func<DbConnection, IDbTransaction, TModelType> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var result = operation(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch
        {
            SafeRollback(transaction);
            throw;
        }
    }

    protected void ExecuteInTransaction(Action<DbConnection, IDbTransaction> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            operation(connection, transaction);
            transaction.Commit();
        }
        catch
        {
            SafeRollback(transaction);
            throw;
        }
    }

    protected TResultType? ExecuteScalar<TResultType>(string scriptName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var sql = ScriptCache.GetScript(scriptName);
            var result = connection.ExecuteScalar<TResultType>(sql, param, transaction, CommandTimeout);
            transaction.Commit();
            return result;
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString ?? ConnectionString), scriptName, ex);
        }
    }

    protected ReadOnlyCollection<TModelType> ExecuteStoredProcedure(string procedureName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        return new ReadOnlyCollection<TModelType>(
            connection.Query<TModelType>(procedureName, param, commandType: CommandType.StoredProcedure).ToList());
    }

    protected void ExecuteStoredProcedureNonQuery(string procedureName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            ExecuteInternal(connection, procedureName, param, transaction, CommandType.StoredProcedure);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString ?? ConnectionString), procedureName, ex);
        }
    }

    protected ReadOnlyCollection<TModelType> Query(string scriptName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        var sql = ScriptCache.GetScript(scriptName);
        var result = connection.Query<TModelType>(sql, param).ToList();
        return new ReadOnlyCollection<TModelType>(result);
    }

    protected TModelType? QuerySingleOrDefault(string scriptName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        var sql = ScriptCache.GetScript(scriptName);
        return connection.QuerySingleOrDefault<TModelType>(sql, param);
    }

    protected dynamic? QuerySingleOrDefaultWithDynamic(string scriptName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        var sql = ScriptCache.GetScript(scriptName);
        return connection.QuerySingleOrDefault<dynamic>(sql, param);
    }

    public ReadOnlyCollection<TModelType> SelectAll() => Query(SelectAllScriptName);

    public ReadOnlyCollection<TModelType> SelectWhere(object param) => Query(SelectWhereScriptName, param);

    public void Insert(TModelType? model) => Execute(InsertScriptName, param: model);

    public void Update(TModelType? model) => Execute(UpdateScriptName, param: model);

    public void DeleteWhere(object? param) => Execute(DeleteWhereScriptName, param: param);

    public abstract TModelType SelectByKey(TKey key);

    public abstract void DeleteByKey(TKey key);

    IEnumerable<TModelType> IRepositoryWithCompositeKey<TModelType, TKey>.SelectAll() => SelectAll();
}
