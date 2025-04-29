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

namespace Mtf.Database
{
    public abstract class BaseRepository<TModelType> : BaseRepository, IRepository<TModelType>
    {
        private static readonly string SelectScriptName = $"{nameof(Select)}{typeof(TModelType).Name}";
        private static readonly string SelectAllScriptName = $"{nameof(SelectAll)}{typeof(TModelType).Name}";
        private static readonly string SelectWhereScriptName = $"{nameof(SelectWhere)}{typeof(TModelType).Name}";
        private static readonly string InsertScriptName = $"{nameof(Insert)}{typeof(TModelType).Name}";
        private static readonly string UpdateScriptName = $"{nameof(Update)}{typeof(TModelType).Name}";
        private static readonly string DeleteScriptName = $"{nameof(Delete)}{typeof(TModelType).Name}";
        private static readonly string DeleteWhereScriptName = $"{nameof(DeleteWhere)}{typeof(TModelType).Name}";

        private static readonly Dictionary<Type, string> TypeMapping = new Dictionary<Type, string>
        {
            { typeof(short), "SMALLINT" },
            { typeof(int), "INT" },
            { typeof(long), "BIGINT" },
            { typeof(byte), "TINYINT" },
            { typeof(decimal), "DECIMAL" },
            { typeof(double), "FLOAT" },
            { typeof(float), "REAL" },
            { typeof(bool), "BIT" }
        };

        static BaseRepository()
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var lastScript = String.Empty;

                    try
                    {
                        foreach (var script in ScriptsToExecute)
                        {
                            lastScript = script;
                            var sql = ResourceHelper.GetDbScript(script);
                            _ = connection.Execute(sql, transaction: transaction, commandTimeout: CommandTimeout);
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

        protected TResultType ExecuteScalar<TResultType>(string scriptName, object param = null)
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
                        var sql = ResourceHelper.GetDbScript(scriptName);
                        var result = connection.ExecuteScalar<TResultType>(sql, param, transaction, CommandTimeout);
                        transaction.Commit();
                        return result;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new SqlScriptExecutionException(lastScript, ex);
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
                    var lastScript = String.Empty;

                    try
                    {
                        lastScript = procedureName;
                        _ = connection.Execute(procedureName, param, transaction, CommandTimeout, CommandType.StoredProcedure);
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

        protected TModelType QuerySingleOrDefault(string scriptName, long id)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.QuerySingleOrDefault<TModelType>(ResourceHelper.GetDbScript(scriptName), new { Id = id });
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

        protected TModelType QuerySingleOrDefault(string scriptName, object param = null)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                return connection.QuerySingleOrDefault<TModelType>(ResourceHelper.GetDbScript(scriptName), param);
            }
        }

        public TModelType Select(long id)
        {
            return QuerySingleOrDefault(SelectScriptName, id);
        }

        public TModelType Select(int id)
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

        public void Insert(TModelType model)
        {
            Execute(InsertScriptName, model);
        }

        public T InsertAndReturnId<T>(TModelType model) where T : struct
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var lastScript = String.Empty;

                    try
                    {
                        lastScript = InsertScriptName;
                        var typeName = TypeMapping.ContainsKey(typeof(T)) ? TypeMapping[typeof(T)] : typeof(T).Name.ToUpperInvariant();
                        var query = $"{ResourceHelper.GetDbScript(InsertScriptName)}; SELECT CAST(SCOPE_IDENTITY() AS {typeName});";
                        var result = connection.ExecuteScalar<T>(query, model, transaction, CommandTimeout);
                        transaction.Commit();
                        return result;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new SqlScriptExecutionException(lastScript, ex);
                    }
                }
            }
        }

        public void Update(TModelType model)
        {
            Execute(UpdateScriptName, model);
        }

        public void Delete(long id)
        {
            Execute(DeleteScriptName, new { Id = id });
        }

        public void Delete(int id)
        {
            Execute(DeleteScriptName, new { Id = id });
        }

        public void DeleteWhere(object param)
        {
            Execute(DeleteWhereScriptName, param);
        }
    }
}
