﻿using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Mtf.Database.Enums;
using Mtf.Database.Models;
using Mtf.Database.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;

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
                case DbProviderType.SQLite:
                    return new SqliteConnection(ConnectionString);

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
                _ = connection.Execute(ResourceHelper.GetDbScript(scriptName), param, commandTimeout: CommandTimeout);
            }
        }

        public static void Execute(string scriptName, object param = null)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        _ = connection.Execute(ResourceHelper.GetDbScript(scriptName), param, transaction, CommandTimeout);
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
                    try
                    {
                        foreach (var parameter in parameters)
                        {
                            _ = connection.Execute(ResourceHelper.GetDbScript(parameter.ScriptName), parameter.Param, transaction, CommandTimeout);
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

        public static string ExecuteScalarQuery(string query)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.ExecuteScalar<string>(query, commandTimeout: CommandTimeout);
            }
        }
    }

    public abstract class BaseRepository<TModelType> : BaseRepository
    {
        static BaseRepository()
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var script in ScriptsToExecute)
                        {
                            _ = connection.Execute(ResourceHelper.GetDbScript(script), transaction: transaction, commandTimeout: CommandTimeout);
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

        protected TModelType ExecuteInTransaction(Func<DbConnection, IDbTransaction, TModelType> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

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
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

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
                        var result = connection.ExecuteScalar<TResultType>(ResourceHelper.GetDbScript(scriptName), param, transaction, CommandTimeout);
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

        protected ReadOnlyCollection<TModelType> ExecuteStoredProcedure(string procedureName, object param = null)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return new ReadOnlyCollection<TModelType>(
                    connection.Query<TModelType>(procedureName, param, commandType: CommandType.StoredProcedure).ToList()
                );
            }
        }

        protected void ExecuteStoredProcedureNonQuery(string procedureName, object param = null)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        _ = connection.Execute(procedureName, param, transaction, CommandTimeout, CommandType.StoredProcedure);
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

        protected ReadOnlyCollection<TModelType> Query(string scriptName)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return new ReadOnlyCollection<TModelType>(connection.Query<TModelType>(ResourceHelper.GetDbScript(scriptName)).ToList());
            }
        }

        protected ReadOnlyCollection<TModelType> Query(string scriptName, object param)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return new ReadOnlyCollection<TModelType>(connection.Query<TModelType>(ResourceHelper.GetDbScript(scriptName), param).ToList());
            }
        }

        protected TModelType QuerySingleOrDefault(string scriptName, int id)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.QuerySingleOrDefault<TModelType>(ResourceHelper.GetDbScript(scriptName), new { Id = id });
            }
        }

        protected TModelType QuerySingleOrDefault(string scriptName, object param)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.QuerySingleOrDefault<TModelType>(ResourceHelper.GetDbScript(scriptName), param);
            }
        }

        public TModelType Get(int id)
        {
            var queryName = $"Select{typeof(TModelType).Name}";
            return QuerySingleOrDefault(queryName, id);
        }

        public ReadOnlyCollection<TModelType> GetAll()
        {
            var queryName = $"SelectAll{typeof(TModelType).Name}";
            return Query(queryName);
        }

        public ReadOnlyCollection<TModelType> GetWhere(object param)
        {
            var queryName = $"SelectAll{typeof(TModelType).Name}";
            return Query(queryName, param);
        }

        public void Insert(TModelType model)
        {
            var scriptName = $"Insert{typeof(TModelType).Name}";
            Execute(scriptName, model);
        }

        public int InsertAndReturnId(TModelType model)
        {
            var scriptName = $"Insert{typeof(TModelType).Name}";

            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var result = connection.ExecuteScalar<int>(ResourceHelper.GetDbScript(scriptName) + "; SELECT CAST(SCOPE_IDENTITY() AS INT);", model, transaction, CommandTimeout);
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

        public void Update(TModelType model)
        {
            var scriptName = $"Update{typeof(TModelType).Name}";
            Execute(scriptName, model);
        }

        public void Delete(int id)
        {
            var scriptName = $"Delete{typeof(TModelType).Name}";
            Execute(scriptName, new { Id = id });
        }

        public void DeleteWhere(object param)
        {
            var scriptName = $"Delete{typeof(TModelType).Name}";
            Execute(scriptName, param);
        }
    }
}
