using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using Dapper;
using System.Linq;
using Mtf.Database.Models;
using Mtf.Database.Services;
using Mtf.Database.Enums;
using System.Data.SQLite;
using System.Reflection;

namespace Mtf.Database
{
    public class BaseRepository
    {
        public static string ConnectionString { get; set; }

        public static DbProviderType DbProvider { get; set; } = DbProviderType.SqlServer;

        public static List<string> ScriptsToExecute { get; set; }

        public static Assembly DatabaseScriptsAssembly { get; set; }

        public static string DatabaseScriptsLocation { get; set; }
    }

    public abstract class BaseRepository<T> : BaseRepository
    {
        private static DbConnection CreateConnection()
        {
            switch (DbProvider)
            {
                case DbProviderType.SQLite:
                    return new SQLiteConnection(ConnectionString);

                case DbProviderType.SqlServer:
                    return new SqlConnection(ConnectionString);

                default:
                    throw new NotSupportedException("Database provider not supported.");
            }
        }

        static BaseRepository()
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var script in BaseRepository.ScriptsToExecute)
                        {
                            connection.Execute(ResourceHelper.GetDbScript(script), transaction: transaction);
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        protected T ExecuteInTransaction(Func<DbConnection, IDbTransaction, T> operation)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var result = operation(connection, transaction);
                        transaction.Commit();
                        return result;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        protected void ExecuteInTransaction(Action<DbConnection, IDbTransaction> operation)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        operation(connection, transaction);
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        protected TResultType ExecuteScalar<TResultType>(string scriptName, object param)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var result = connection.ExecuteScalar<TResultType>(ResourceHelper.GetDbScript(scriptName), param, transaction);
                        transaction.Commit();
                        return result;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        protected void Execute(string scriptName, object param)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        connection.Execute(ResourceHelper.GetDbScript(scriptName), param, transaction);
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        protected void Execute(params SqlParam[] parameters)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var parameter in parameters)
                        {
                            connection.Execute(ResourceHelper.GetDbScript(parameter.ScriptName), parameter.Param, transaction);
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        protected List<T> Query(string scriptName)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.Query<T>(ResourceHelper.GetDbScript(scriptName)).ToList();
            }
        }

        protected List<T> Query(string scriptName, object param)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.Query<T>(ResourceHelper.GetDbScript(scriptName), param).ToList();
            }
        }

        protected T QuerySingleOrDefault(string scriptName, int id)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.QuerySingleOrDefault<T>(ResourceHelper.GetDbScript(scriptName), new { Id = id });
            }
        }

        protected T QuerySingleOrDefault(string scriptName, object param)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.QuerySingleOrDefault<T>(ResourceHelper.GetDbScript(scriptName), param);
            }
        }

        public T Get(int id)
        {
            var queryName = $"Select{typeof(T).Name}";
            return QuerySingleOrDefault(queryName, id);
        }

        public List<T> GetAll()
        {
            var queryName = $"SelectAll{typeof(T).Name}";
            return Query(queryName);
        }

        public List<T> GetWhere(object param)
        {
            var queryName = $"SelectAll{typeof(T).Name}";
            return Query(queryName, param);
        }

        public void Delete(int id)
        {
            var scriptName = $"Delete{typeof(T).Name}";
            Execute(scriptName, new { Id = id });
        }

        public void DeleteWhere(object param)
        {
            var scriptName = $"Delete{typeof(T).Name}";
            Execute(scriptName, param);
        }
    }
}
