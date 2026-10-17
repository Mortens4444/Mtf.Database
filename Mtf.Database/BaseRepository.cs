using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Mtf.Database.Enums;
using Mtf.Database.Exceptions;
using Mtf.Database.Models;
using Mtf.Database.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Mtf.Database;

public partial class BaseRepository(string connectionString)
{
    public string ConnectionString { get; init; } = connectionString;

    public static int? CommandTimeout { get; set; }

    public static DbProviderType DbProvider { get; set; } = DbProviderType.SqlServer;

    public static List<string> ScriptsToExecute { get; } = new List<string>();

    public static Assembly? DatabaseScriptsAssembly { get; set; }

    public static string? DatabaseScriptsLocation { get; set; }

    public static void ExecuteMigrations(string connectionString)
    {
        using var connection = CreateConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var lastScript = String.Empty;

        try
        {
            foreach (var script in ScriptsToExecute)
            {
                lastScript = script;
                var sql = ScriptCache.GetScript(script);
                ExecuteInternal(connection, sql, null, transaction);
            }
            transaction.Commit();
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(connectionString), lastScript, ex);
        }
    }

    protected static DbConnection CreateConnection(string connectionString)
    {
        return DbProvider switch
        {
            DbProviderType.SQLite => new SqliteConnection(connectionString),
            DbProviderType.SqlServer => new SqlConnection(connectionString),
            _ => throw new NotSupportedException("Database provider not supported."),
        };
    }

    protected DbConnection CreateConnection()
    {
        return CreateConnection(ConnectionString);
    }

    public void ExecuteWithoutTransaction(string scriptName, object? param = null)
    {
        var sql = ScriptCache.GetScript(scriptName);
        ValidateParameters(sql, param);

        using var connection = CreateConnection();
        connection.Open();
        ExecuteInternal(connection, sql, param);
    }

    public void Execute(string scriptName, object? param = null)
    {
        var sql = LoadScript(scriptName);
        ValidateParameters(sql, param);

        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            ExecuteInternal(connection, sql, param, transaction);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString), scriptName, ex);
        }
    }

    public async Task ExecuteAsync(string scriptName, object? param = null)
    {
        var sql = LoadScript(scriptName);
        ValidateParameters(sql, param);

        using var connection = CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

        try
        {
            await ExecuteInternalAsync(connection, sql, param, transaction).ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await SafeRollbackAsync(transaction).ConfigureAwait(false);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString), scriptName, ex);
        }
    }

    public void Execute(params SqlParam[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
        {
            return;
        }

        // Load and validate everything up front, so a bad script or value fails before any I/O happens.
        var scripts = new List<(string ScriptName, string Sql, object? Param)>(parameters.Length);
        foreach (var parameter in parameters)
        {
            if (parameter != null)
            {
                var sql = LoadScript(parameter.ScriptName);
                ValidateParameters(sql, parameter.Param);
                scripts.Add((parameter.ScriptName, sql, parameter.Param));
            }
        }

        if (scripts.Count == 0)
        {
            return;
        }

        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var lastScript = String.Empty;

        try
        {
            foreach (var (scriptName, sql, param) in scripts)
            {
                lastScript = scriptName;
                ExecuteInternal(connection, sql, param, transaction);
            }
            transaction.Commit();
        }
        catch (Exception ex)
        {
            SafeRollback(transaction);
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString), lastScript, ex);
        }
    }

    /// <summary>
    /// Loads a script from the cache, reporting a missing or unreadable script as a
    /// <see cref="SqlScriptExecutionException"/> for the script, like any other execution failure.
    /// </summary>
    protected string LoadScript(string scriptName)
    {
        try
        {
            return ScriptCache.GetScript(scriptName);
        }
        catch (Exception ex)
        {
            throw new SqlScriptExecutionException(Utils.GetDatabaseName(ConnectionString), scriptName, ex);
        }
    }

    public string? ExecuteScalarQuery(string query)
    {
        using var connection = CreateConnection();
        connection.Open();
        return connection.ExecuteScalar<string>(query, commandTimeout: CommandTimeout);
    }

    public DataTable ExecuteQuery(string query, Dictionary<string, object>? parameters = null)
    {
        if (!IsQuerySeemsSafe(query))
        {
            throw new ArgumentException("The SQL query contains potentially unsafe content.");
        }

        using var connection = CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = query ?? String.Empty;
        command.CommandTimeout = CommandTimeout ?? 30;

        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = param.Key;
                parameter.Value = param.Value ?? DBNull.Value;
                _ = command.Parameters.Add(parameter);
            }
        }

        var dataTable = new DataTable();
        connection.Open();

        using (var reader = command.ExecuteReader())
        {
            dataTable.Load(reader);
        }

        return dataTable;
    }

    public static bool IsQuerySeemsSafe(string query)
    {
        if (String.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var dangerousKeywords = new[]
        {
            "drop ", "delete ", "insert ", "update ", "--", ";", "/*", "*/", "xp_"
        };

        foreach (var keyword in dangerousKeywords)
        {
            if (query.IndexOf(keyword, 0, query.Length, StringComparison.OrdinalIgnoreCase) != -1)
            {
                return false;
            }
        }

        var singleQuotes = query.Count(c => c == '\'');
        var doubleQuotes = query.Count(c => c == '"');
        if (singleQuotes % 2 != 0 || doubleQuotes % 2 != 0)
        {
            return false;
        }

        return true;
    }

    public bool HasValidSyntax(string scriptName, bool checkDeclarations, out Exception? exception)
    {
        var sql = ScriptCache.GetScript(scriptName);
        return HasValidSqlSyntax(sql, checkDeclarations, out exception);
    }

    public bool HasValidSqlSyntax(string sql, bool checkDeclarations, out Exception? exception)
    {
        using var connection = CreateConnection();
        connection.Open();
        try
        {
            var batches = BatchesRegex().Split(sql);
            foreach (var batch in batches.Where(batch => !String.IsNullOrWhiteSpace(batch)))
            {
                using var command = connection.CreateCommand();
                CheckSyntax(batch, checkDeclarations, command);
            }
            exception = null;
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }

    private static void CheckSyntax(string sql, bool checkDeclarations, DbCommand command)
    {
        var isProcDefinition = IsProcDefinition().IsMatch(sql);

        if (!checkDeclarations && !isProcDefinition)
        {
            var usageRegex = IsIdentifier();
            var allUsedParameters = usageRegex.Matches(sql)
                                              .Cast<Match>()
                                              .Select(m => m.Value)
                                              .Distinct(StringComparer.OrdinalIgnoreCase);

            var declarationRegex = IsDeclaration();
            var alreadyDeclaredParameters = declarationRegex.Matches(sql)
                                                             .Cast<Match>()
                                                             .Select(m => m.Groups[1].Value)
                                                             .Distinct(StringComparer.OrdinalIgnoreCase);

            var parametersToDeclare = allUsedParameters.Except(alreadyDeclaredParameters);

            var declarations = String.Join(" ", parametersToDeclare.Select(p => $"DECLARE {p} NVARCHAR(MAX);"));

            foreach (var p in parametersToDeclare)
            {
                var pattern = "IN " + p;
                if (sql.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    sql = Regex.Replace(sql, $@"IN\s*{Regex.Escape(p)}", $"IN (SELECT value FROM STRING_SPLIT({p}, ','))", RegexOptions.IgnoreCase);
                }
            }

            command.CommandText = $"SET PARSEONLY ON; {declarations} {sql}";
        }
        else
        {
            command.CommandText = $"SET PARSEONLY ON; {sql}";
        }

        command.ExecuteNonQuery();
    }

    public int GetDatabaseUsagePercentageWithLimit()
    {
        var engineEdition = Convert.ToInt32(ExecuteScalarQuery("SELECT SERVERPROPERTY('EngineEdition')"), CultureInfo.InvariantCulture);
        var maxSizeInBytes = engineEdition == 4 ? 10L * 1024 * 1024 * 1024 : Int64.MaxValue;

        using var result = ExecuteQuery("EXEC sp_spaceused");
        var firstRow = result.Rows[0];
        var databaseSize = ParseSize(firstRow["database_size"].ToString());
        var unallocatedSpace = ParseSize(firstRow["unallocated space"].ToString());

        if (databaseSize > 0)
        {
            var usedSpace = databaseSize - unallocatedSpace;
            var usagePercentage = (int)Math.Round(usedSpace / maxSizeInBytes * 100);
            return usagePercentage;
        }

        return -1;
    }

    protected static void SafeRollback(DbTransaction? transaction)
    {
        try
        {
            transaction?.Rollback();
        }
        catch { }
    }

    protected static async Task SafeRollbackAsync(DbTransaction? transaction)
    {
        try
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Executes <paramref name="sql"/> through Dapper with <paramref name="param"/> bound as-is (objects, dictionaries,
    /// collections for multi-exec and <see cref="DynamicParameters"/> all work the way they do in Dapper).
    /// No parameter sizes are set, so an over-long value is rejected by the database instead of being truncated.
    /// Declared lengths are validated by the public repository methods before any I/O; callers of this method
    /// can do the same with the repository's validation step.
    /// </summary>
    protected static int ExecuteInternal(IDbConnection connection, string sql, object? param = null, IDbTransaction? transaction = null, CommandType? commandType = null)
    {
        return connection.Execute(sql, param, transaction, commandTimeout: CommandTimeout, commandType: commandType);
    }

    protected static Task<int> ExecuteInternalAsync(IDbConnection connection, string sql, object? param = null, IDbTransaction? transaction = null, CommandType? commandType = null)
    {
        return connection.ExecuteAsync(sql, param, transaction, commandTimeout: CommandTimeout, commandType: commandType);
    }

    /// <summary>
    /// Validates the declared maximum lengths (<see cref="Attributes.LengthAttribute"/>, <c>[MaxLength]</c>,
    /// <c>[StringLength]</c>) of the string properties of <paramref name="param"/> that <paramref name="sql"/>
    /// references. Stored procedures bind every property, so pass <see cref="CommandType.StoredProcedure"/> for them.
    /// </summary>
    /// <exception cref="ValueTooLongException">A string property is longer than its declared maximum.</exception>
    protected static void ValidateParameters(string sql, object? param, CommandType commandType = CommandType.Text)
    {
        ParameterValidator.Validate(sql, param, commandType);
    }

    private static double ParseSize(string? size)
    {
        if (String.IsNullOrWhiteSpace(size))
        {
            return 0;
        }

        var sizeValue = Double.Parse(size.AsSpan(0, size.Length - 3), CultureInfo.InvariantCulture);
        var sizeUnit = size[^2..];

        return sizeUnit switch
        {
            "KB" => sizeValue * 1024,
            "MB" => sizeValue * 1024 * 1024,
            "GB" => sizeValue * 1024 * 1024 * 1024,
            "TB" => sizeValue * 1024 * 1024 * 1024 * 1024,
            _ => sizeValue,
        };
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline, "hu-HU")]
    private static partial Regex BatchesRegex();
    
    [GeneratedRegex(@"^\s*(CREATE|ALTER)\s+PROC(EDURE)?", RegexOptions.IgnoreCase | RegexOptions.Multiline, "hu-HU")]
    private static partial Regex IsProcDefinition();
    
    [GeneratedRegex(@"@[a-zA-Z_][a-zA-Z0-9_]*")]
    private static partial Regex IsIdentifier();
    
    [GeneratedRegex(@"DECLARE\s+(@[a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.IgnoreCase, "hu-HU")]
    private static partial Regex IsDeclaration();
}
