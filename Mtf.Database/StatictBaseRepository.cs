using Dapper;
using System;
#if NET452
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
#endif
using Mtf.Database.Enums;
using Mtf.Database.Models;
using Mtf.Database.Services;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Mtf.Database.Exceptions;

namespace Mtf.Database
{
    public abstract class BaseRepository
    {
        public static string ConnectionString { get; set; }

        public static int? CommandTimeout { get; set; }

        public static DbProviderType DbProvider { get; set; } = DbProviderType.SqlServer;

        public static List<string> ScriptsToExecute { get; } = new List<string>();

        public static Assembly DatabaseScriptsAssembly { get; set; }

        public static string DatabaseScriptsLocation { get; set; }

        protected static DbConnection CreateConnection()
        {
            switch (DbProvider)
            {
#if !NET452
                case DbProviderType.SQLite:
                    return new SqliteConnection(ConnectionString);
#endif

                case DbProviderType.SqlServer:
                    return new SqlConnection(ConnectionString);

                default:
                    throw new NotSupportedException("Database provider not supported.");
            }
        }

        public static void ExecuteWithoutTransaction(string scriptName, object param = null)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                _ = connection.Execute(ScriptCache.GetScript(scriptName), param, commandTimeout: CommandTimeout);
            }
        }

        public static void Execute(string scriptName, object param = null)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var lastScript = String.Empty;

                    try
                    {
                        lastScript = scriptName;
                        var sql = ScriptCache.GetScript(scriptName);
                        _ = connection.Execute(sql, param, transaction, CommandTimeout);
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new SqlScriptExecutionException(lastScript, ex);
                    }
                }
            }
        }

        public static void Execute(params SqlParam[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return;
            }

            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var lastScript = String.Empty;

                    try
                    {
                        foreach (var parameter in parameters)
                        {
                            lastScript = parameter.ScriptName;
                            var sql = ScriptCache.GetScript(parameter.ScriptName);
                            _ = connection.Execute(sql, parameter.Param, transaction, CommandTimeout);
                        }
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new SqlScriptExecutionException(lastScript, ex);
                    }
                }
            }
        }

        public static string ExecuteScalarQuery(string query)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.ExecuteScalar<string>(query, commandTimeout: CommandTimeout);
            }
        }

        public static DataTable ExecuteQuery(string query, Dictionary<string, object> parameters = null)
        {
            if (!IsQuerySeemsSafe(query))
            {
                throw new ArgumentException("The SQL query contains potentially unsafe content.");
            }

            using (var connection = CreateConnection())
            using (var command = connection.CreateCommand())
            {
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

        public static int GetDatabaseUsagePercentageWithLimit()
        {
            var engineEdition = Convert.ToInt32(ExecuteScalarQuery("SELECT SERVERPROPERTY('EngineEdition')"), CultureInfo.InvariantCulture);
            var maxSizeInBytes = engineEdition == 4 ? 10L * 1024 * 1024 * 1024 : Int64.MaxValue;

            using (var result = ExecuteQuery("EXEC sp_spaceused"))
            {
                var firstRow = result.Rows[0];
                var databaseSize = ParseSize(firstRow["database_size"].ToString());
                var unallocatedSpace = ParseSize(firstRow["unallocated space"].ToString());

                if (databaseSize > 0)
                {
                    var usedSpace = databaseSize - unallocatedSpace;
                    var usagePercentage = (int)Math.Round(usedSpace / maxSizeInBytes * 100);
                    return usagePercentage;
                }
            }

            return -1;
        }

        private static double ParseSize(string size)
        {
            if (String.IsNullOrWhiteSpace(size))
            {
                return 0;
            }

            var sizeValue = Double.Parse(size.Substring(0, size.Length - 3), CultureInfo.InvariantCulture);
            var sizeUnit = size.Substring(size.Length - 2);

            switch (sizeUnit)
            {
                case "KB":
                    return sizeValue * 1024;
                case "MB":
                    return sizeValue * 1024 * 1024;
                case "GB":
                    return sizeValue * 1024 * 1024 * 1024;
                default:
                    return sizeValue;
            }
        }
    }
}
