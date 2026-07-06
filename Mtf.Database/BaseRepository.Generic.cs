using Dapper;
using Mtf.Database.Exceptions;
using Mtf.Database.Interfaces;
using Mtf.Database.Services;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace Mtf.Database;

public abstract class BaseRepository<TModelType> : BaseRepository, IRepository<TModelType>
{
    public string ScriptsSubfolderName { get; } = typeof(TModelType).Name;

    private readonly string SelectScriptName;
    private readonly string SelectAllScriptName;
    private readonly string SelectWhereScriptName;
    private readonly string InsertScriptName;
    private readonly string UpdateScriptName;
    private readonly string DeleteScriptName;
    private readonly string DeleteWhereScriptName;

    protected BaseRepository(string connectionString) : base(connectionString)
    {
        SelectScriptName = $"{ScriptsSubfolderName}.{nameof(Select)}{typeof(TModelType).Name}";
        SelectAllScriptName = $"{ScriptsSubfolderName}.{nameof(SelectAll)}{typeof(TModelType).Name}";
        SelectWhereScriptName = $"{ScriptsSubfolderName}.{nameof(SelectWhere)}{typeof(TModelType).Name}";
        InsertScriptName = $"{ScriptsSubfolderName}.{nameof(Insert)}{typeof(TModelType).Name}";
        UpdateScriptName = $"{ScriptsSubfolderName}.{nameof(Update)}{typeof(TModelType).Name}";
        DeleteScriptName = $"{ScriptsSubfolderName}.{nameof(Delete)}{typeof(TModelType).Name}";
        DeleteWhereScriptName = $"{ScriptsSubfolderName}.{nameof(DeleteWhere)}{typeof(TModelType).Name}";
    }

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
        var lastScript = String.Empty;

        try
        {
            lastScript = scriptName;
            var sql = ScriptCache.GetScript(scriptName, ScriptsSubfolderName);
            var result = connection.ExecuteScalar<TResultType>(sql, param, transaction, CommandTimeout);
            transaction.Commit();
            return result;
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString ?? ConnectionString), lastScript, ex);
        }
    }

    protected ReadOnlyCollection<TModelType> ExecuteStoredProcedure(string procedureName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        return new ReadOnlyCollection<TModelType>(
            connection.Query<TModelType>(procedureName, param, commandType: CommandType.StoredProcedure).ToList()
        );
    }

    protected void ExecuteStoredProcedureNonQuery(string procedureName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var lastScript = String.Empty;

        try
        {
            lastScript = procedureName;
            ExecuteInternal(connection, procedureName, param, transaction, CommandType.StoredProcedure);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString ?? ConnectionString), lastScript, ex);
        }
    }

    protected ReadOnlyCollection<TModelType> Query(string scriptName)
    {
        using var connection = CreateConnection();
        connection.Open();
        return new ReadOnlyCollection<TModelType>(connection.Query<TModelType>(ScriptCache.GetScript(scriptName, ScriptsSubfolderName)).ToList());
    }

    protected ReadOnlyCollection<TModelType> Query(string scriptName, object param)
    {
        using var connection = CreateConnection();
        connection.Open();
        return new ReadOnlyCollection<TModelType>(connection.Query<TModelType>(ScriptCache.GetScript(scriptName, ScriptsSubfolderName), param).ToList());
    }

    protected TModelType? QuerySingleOrDefault(string scriptName, long id)
    {
        using var connection = CreateConnection();
        connection.Open();
        return connection.QuerySingleOrDefault<TModelType>(ScriptCache.GetScript(scriptName, ScriptsSubfolderName), new { Id = id });
    }

    protected TModelType? QuerySingleOrDefault(string scriptName, int id)
    {
        using var connection = CreateConnection();
        connection.Open();
        return connection.QuerySingleOrDefault<TModelType>(ScriptCache.GetScript(scriptName, ScriptsSubfolderName), new { Id = id });
    }

    protected TModelType? QuerySingleOrDefault(string scriptName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        return connection.QuerySingleOrDefault<TModelType>(ScriptCache.GetScript(scriptName, ScriptsSubfolderName), param);
    }

    protected dynamic? QuerySingleOrDefaultWithDynamic(string scriptName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        return connection.QuerySingleOrDefault<dynamic>(ScriptCache.GetScript(scriptName, ScriptsSubfolderName), param);
    }

    public TModelType? Select(long id)
    {
        return QuerySingleOrDefault(SelectScriptName, id);
    }

    public TModelType? Select(int id)
    {
        return QuerySingleOrDefault(SelectScriptName, id);
    }

    public ReadOnlyCollection<TModelType> SelectAll()
    {
        return Query(SelectAllScriptName);
    }

    public ReadOnlyCollection<TModelType> SelectWhere(object param)
    {
        return Query(SelectWhereScriptName, param);
    }

    public void Insert(TModelType? model)
    {
        Execute(InsertScriptName, param: model);
    }

    public T InsertAndReturnId<T>(TModelType model) where T : struct
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var lastScript = String.Empty;

        try
        {
            lastScript = InsertScriptName;
            var typeName = TypeMapping.Mappings.ContainsKey(typeof(T)) ? TypeMapping.Mappings[typeof(T)] : typeof(T).Name.ToUpperInvariant();
            var query = $"{ScriptCache.GetScript(InsertScriptName, ScriptsSubfolderName)}; SELECT CAST(SCOPE_IDENTITY() AS {typeName});";
            var result = connection.ExecuteScalar<T>(query, model, transaction, CommandTimeout);
            transaction.Commit();
            return result;
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString ?? ConnectionString), lastScript, ex);
        }
    }

    public void Update(TModelType? model)
    {
        Execute(UpdateScriptName, param: model);
    }

    public void Delete(long id)
    {
        Execute(DeleteScriptName, param: new { Id = id });
    }

    public void Delete(int id)
    {
        Execute(DeleteScriptName, param: new { Id = id });
    }

    public void DeleteWhere(object? param)
    {
        Execute(DeleteWhereScriptName, param: param);
    }
}
