using Dapper;
using Microsoft.Extensions.Logging;
using Mtf.Database.Exceptions;
using Mtf.Database.Interfaces;
using Mtf.Database.Services;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace Mtf.Database;

public abstract class BaseRepository<TEntity, TIdentifierType> : BaseRepository, IBaseRepository<TEntity, TIdentifierType>, IRepository<TEntity, TIdentifierType>
    where TEntity : class, IHasIdentifier<TIdentifierType>
{
    public string ScriptsSubfolderName { get; } = typeof(TEntity).Name;

    private readonly string SelectScriptName;
    private readonly string SelectAllScriptName;
    private readonly string SelectWhereScriptName;
    private readonly string InsertScriptName;
    private readonly string UpdateScriptName;
    private readonly string DeleteScriptName;
    private readonly string DeleteWhereScriptName;
    private readonly ILogger<BaseRepository<TEntity, TIdentifierType>>? logger;

    protected BaseRepository(string connectionString) : base(connectionString)
    {
        SelectScriptName = $"{ScriptsSubfolderName}.{nameof(Select)}{typeof(TEntity).Name}";
        SelectAllScriptName = $"{ScriptsSubfolderName}.{nameof(SelectAll)}{typeof(TEntity).Name}";
        SelectWhereScriptName = $"{ScriptsSubfolderName}.{nameof(SelectWhere)}{typeof(TEntity).Name}";
        InsertScriptName = $"{ScriptsSubfolderName}.{nameof(Insert)}{typeof(TEntity).Name}";
        UpdateScriptName = $"{ScriptsSubfolderName}.{nameof(Update)}{typeof(TEntity).Name}";
        DeleteScriptName = $"{ScriptsSubfolderName}.{nameof(Delete)}{typeof(TEntity).Name}";
        DeleteWhereScriptName = $"{ScriptsSubfolderName}.{nameof(DeleteWhere)}{typeof(TEntity).Name}";
    }

    protected BaseRepository(ILogger<BaseRepository<TEntity, TIdentifierType>> logger, string connectionString) : this(connectionString)
    {
        this.logger = logger;
    }

    protected TEntity ExecuteInTransaction(Func<DbConnection, IDbTransaction, TEntity> operation)
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
            var sql = ScriptCache.GetScript(scriptName);
            var result = connection.ExecuteScalar<TResultType>(sql, param, transaction, CommandTimeout);
            transaction.Commit();
            return result;
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString), lastScript, ex);
        }
    }

    protected ReadOnlyCollection<TEntity> ExecuteStoredProcedure(string procedureName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        return new ReadOnlyCollection<TEntity>(
            connection.Query<TEntity>(procedureName, param, commandType: CommandType.StoredProcedure).ToList()
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
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString), lastScript, ex);
        }
    }

    public ReadOnlyCollection<TEntity> Query(string scriptName)
    {
        using var connection = CreateConnection();
        connection.Open();
        return new ReadOnlyCollection<TEntity>(connection.Query<TEntity>(ScriptCache.GetScript(scriptName)).ToList());
    }

    public async Task<ReadOnlyCollection<TEntity>> QueryAsync(string scriptName)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        var results = await connection.QueryAsync<TEntity>(ScriptCache.GetScript(scriptName)).ConfigureAwait(false);
        return new ReadOnlyCollection<TEntity>(results.ToList());
    }

    public ReadOnlyCollection<TEntity> Query(string scriptName, object param)
    {
        using var connection = CreateConnection();
        connection.Open();
        return new ReadOnlyCollection<TEntity>(connection.Query<TEntity>(ScriptCache.GetScript(scriptName), param).ToList());
    }

    public async Task<ReadOnlyCollection<TEntity>> QueryAsync(string scriptName, object param)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        var results = await connection.QueryAsync<TEntity>(ScriptCache.GetScript(scriptName), param).ConfigureAwait(false);
        return new ReadOnlyCollection<TEntity>(results.ToList());
    }

    public TEntity? QuerySingleOrDefault(string scriptName, TIdentifierType id)
    {
        using var connection = CreateConnection();
        connection.Open();
        return connection.QuerySingleOrDefault<TEntity>(ScriptCache.GetScript(scriptName), new { Id = id });
    }

    public TEntity? QuerySingleOrDefault(string scriptName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        return connection.QuerySingleOrDefault<TEntity>(ScriptCache.GetScript(scriptName), param);
    }

    public dynamic? QuerySingleOrDefaultWithDynamic(string scriptName, object? param = null)
    {
        using var connection = CreateConnection();
        connection.Open();
        return connection.QuerySingleOrDefault<dynamic>(ScriptCache.GetScript(scriptName), param);
    }

    public TEntity? Select(TIdentifierType id)
    {
        return QuerySingleOrDefault(SelectScriptName, id);
    }

    public ReadOnlyCollection<TEntity> SelectAll()
    {
        return Query(SelectAllScriptName);
    }

    public ReadOnlyCollection<TEntity> SelectWhere(object param)
    {
        return Query(SelectWhereScriptName, param);
    }

    public void Insert(TEntity? model)
    {
        Execute(InsertScriptName, param: model);
    }

    public T InsertAndReturnId<T>(TEntity model) where T : struct
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var lastScript = String.Empty;

        try
        {
            lastScript = InsertScriptName;
            var typeName = TypeMapping.Mappings.ContainsKey(typeof(T)) ? TypeMapping.Mappings[typeof(T)] : typeof(T).Name.ToUpperInvariant();
            var query = $"{ScriptCache.GetScript(InsertScriptName)}; SELECT CAST(SCOPE_IDENTITY() AS {typeName});";
            var result = connection.ExecuteScalar<T>(query, model, transaction, CommandTimeout);
            transaction.Commit();
            return result;
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString), lastScript, ex);
        }
    }

    public void Update(TEntity? model)
    {
        Execute(UpdateScriptName, param: model);
    }

    public void Delete(TIdentifierType id)
    {
        Execute(DeleteScriptName, param: new { Id = id });
    }

    public void DeleteWhere(object? param)
    {
        Execute(DeleteWhereScriptName, param: param);
    }

    public Task<ReadOnlyCollection<TEntity>> GetAllAsync()
    {
        return QueryAsync(SelectAllScriptName);
    }

    public Task<ReadOnlyCollection<TEntity>> GetAllWhereAsync(object param)
    {
        return QueryAsync(SelectWhereScriptName, param);
    }

    public async Task<TEntity?> InsertAsync(TEntity entity)
    {
        try
        {
            await ExecuteAsync(InsertScriptName, entity).ConfigureAwait(false);
            
            using var connection = CreateConnection();
            await connection.OpenAsync().ConfigureAwait(false);
            return await connection.QuerySingleOrDefaultAsync<TEntity>(
                ScriptCache.GetScript(SelectScriptName), new { entity.Id }
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.Log(ex);
            throw;
        }
    }

    public async Task<TEntity?> UpdateAsync(TEntity entity)
    {
        try
        {
            await ExecuteAsync(UpdateScriptName, entity).ConfigureAwait(false);
            
            using var connection = CreateConnection();
            await connection.OpenAsync().ConfigureAwait(false);
            return await connection.QuerySingleOrDefaultAsync<TEntity>(
                ScriptCache.GetScript(SelectScriptName), new { entity.Id }
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.Log(ex);
            throw;
        }
    }

    public async Task<TEntity?> GetByIdAsync(TIdentifierType id)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<TEntity>(ScriptCache.GetScript(SelectScriptName), new { Id = id }).ConfigureAwait(false);
    }

    public Task DeleteAsync(TIdentifierType id)
    {
        return ExecuteAsync(DeleteScriptName, new { Id = id });
    }
}
