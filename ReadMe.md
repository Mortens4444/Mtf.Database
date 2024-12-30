# Mtf.Database BaseRepository Documentation

## Overview

The `Mtf.Database` library provides an abstract `BaseRepository` class for performing CRUD (Create, Read, Update, Delete) operations in SQL databases. It supports both SQLite and SQL Server, with the ability to switch between them based on a type parameter. The repository uses Dapper for simplified data access and manages SQL scripts for database operations within transactions.

This documentation covers setup, constructors, properties, methods, and example usage for `BaseRepository`, providing flexibility and control over database interactions in .NET applications.

## Installation

To use `Mtf.Database`, add the package to your project:

1. **Add Package**:
   Run the following command in your project directory:

   ```bash
   dotnet add package Mtf.Database
   ```

2. **Include the Namespace**:
   Add the `Mtf.Database` namespace at the beginning of your code:

   ```csharp
   using Mtf.Database;
   ```

## **Class: `BaseRepository`**

The `BaseRepository` class serves as the base for managing database operations. It supports automatic transaction handling, script execution, and type-safe operations.

### **Static Properties**

| Property                  | Type             | Description                                                |
|---------------------------|------------------|------------------------------------------------------------|
| `DbProvider`              | `DbProviderType` | Specifies the database provider (`SQLite` or `SqlServer`). |
| `ConnectionString`        | `string`         | Connection string for the database.                        |
| `ScriptsToExecute`        | `List<string>`   | SQL scripts to execute during initialization.              |
| `DatabaseScriptsAssembly` | `Assembly`       | Assembly containing embedded SQL scripts.                  |
| `DatabaseScriptsLocation` | `string`         | Namespace location of embedded SQL scripts.                |

---

### Enum: DbProviderType

Defines the database types supported by `BaseRepository`.

| Value       | Description                             |
|-------------|-----------------------------------------|
| `SQLite`    | SQLite database engine.                 |
| `SqlServer` | SQL Server database engine.             |

### **Static Methods**

#### **Transactional Script Execution**

- **`Execute(string scriptName, object param = null)`**  
  Executes a script wrapped in a transaction.

  ```csharp
  BaseRepository.Execute("UpdateServer", new { Id = 1, Name = "UpdatedServer" });
  ```

- **`ExecuteWithoutTransaction(string scriptName, object param = null)`**  
  Executes a script without a transaction.

  ```csharp
  BaseRepository.ExecuteWithoutTransaction("InsertServer", new { Name = "NewServer" });
  ```

- **`Execute(params SqlParam[] parameters)`**  
  Executes multiple scripts in a single transaction. Each `SqlParam` includes the script name and parameters.

  ```csharp
  BaseRepository.Execute(
      new SqlParam("UpdateServer", new { Id = 1, Name = "UpdatedServer" }),
      new SqlParam("DeleteServer", new { Id = 2 })
  );
  ```

---

## Class: BaseRepository<T>

The `BaseRepository` class provides common database operations, such as executing scripts and querying data. The class supports both SQLite and SQL Server, with automatic handling of transactions to ensure data consistency.

### **Instance Methods (`BaseRepository<T>`)**

#### **Transaction Handling**

- **`T ExecuteInTransaction(Func<DbConnection, IDbTransaction, T> operation)`**  
  Executes a transactional operation that returns a result.

  ```csharp
  var result = repository.ExecuteInTransaction((connection, transaction) =>
  {
      // Perform database actions
      return someResult;
  });
  ```

- **`void ExecuteInTransaction(Action<DbConnection, IDbTransaction> operation)`**  
  Executes a transactional operation without returning a result.

  ```csharp
  repository.ExecuteInTransaction((connection, transaction) =>
  {
      // Perform database actions
  });
  ```

---

#### **Query and Execution**

- **`TModelType QuerySingleOrDefault(string scriptName, object param)`**  
  Returns a single record or `null` if none exist.

  ```csharp
  var server = repository.QuerySingleOrDefault("SelectServerById", new { Id = 1 });
  ```

- **`ReadOnlyCollection<TModelType> Query(string scriptName, object param)`**  
  Returns a list of records matching the parameters.

  ```csharp
  var servers = repository.Query("SelectServersByLocation", new { Location = "Europe" });
  ```

- **`TResultType ExecuteScalar<TResultType>(string scriptName, object param)`**  
  Executes a script and returns a scalar value.

  ```csharp
  int serverCount = repository.ExecuteScalar<int>("CountServers", null);
  ```

---

#### **Stored Procedures**

- **`ReadOnlyCollection<TModelType> ExecuteStoredProcedure(string procedureName, object param = null)`**  
  Executes a stored procedure and returns a list of results.

  ```csharp
  var servers = repository.ExecuteStoredProcedure("GetAllServers", new { Status = "Active" });
  ```

- **`void ExecuteStoredProcedureNonQuery(string procedureName, object param = null)`**  
  Executes a stored procedure without returning results.

  ```csharp
  repository.ExecuteStoredProcedureNonQuery("DeactivateServer", new { Id = 1 });
  ```

---

#### CRUD Operations

- **`T Select(int id)`**  
  Retrieves a single record by ID.

- **`List<T> SelectAll()`**  
  Retrieves all records for the entity.

- **`List<T> SelectWhere(object param)`**  
  Retrieves records that match the specified parameters.

- **`void Delete(int id)`**  
  Deletes a single record by ID.

- **`void DeleteWhere(object param)`**  
  Deletes records matching specified parameters.

### Example Usage

```csharp
using Mtf.Database;
using System.Collections.Generic;

public class ExampleUsage
{
    public static void Main()
    {
        // Configure the repository
        BaseRepository.DatabaseScriptsAssembly = typeof(CameraRepository<>).Assembly;
        BaseRepository.DatabaseScriptsLocation = "Database.Scripts";

        BaseRepository.ConnectionString = ConfigurationManager.ConnectionStrings["MasterConnectionString"]?.ConnectionString;
        BaseRepository.ExecuteWithoutTransaction("CreateDatabase");
        BaseRepository.ExecuteWithoutTransaction("CreateUser");

        BaseRepository.ConnectionString = ConfigurationManager.ConnectionStrings["LiveViewConnectionString"]?.ConnectionString;
        BaseRepository.ExecuteWithoutTransaction("CreateTables");

        // Add initialization scripts
        BaseRepository.ScriptsToExecute.Add("Migration_1");

        // Perform database operations
        var repository = new ServerRepository();

        // Insert a new server
        BaseRepository.Execute("InsertServer", new { Name = "NewServer", Location = "USA" });

        // Retrieve all servers
        var servers = repository.GetAll();
        foreach (var server in servers)
        {
            Console.WriteLine($"Server: {server.Name}, Location: {server.Location}");
        }

        // Delete a server
        repository.Delete(1);
    }
}
```

### Notes

1. **Enhanced Transaction Management**: Automatic transaction rollback on failure ensures data consistency.
2. **Flexible Execution**: Execute both SQL scripts and stored procedures.
3. **Reusable Scripts**: Centralized SQL script management using embedded resources.