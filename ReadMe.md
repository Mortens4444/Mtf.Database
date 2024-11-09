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

## Class: BaseRepository<T>

The `BaseRepository` class provides common database operations, such as executing scripts and querying data. The class supports both SQLite and SQL Server, with automatic handling of transactions to ensure data consistency.

### Constructors

The `BaseRepository` class provides two constructors to support different database engines:

- **`BaseRepository()`**  
  Uses SQLite as the default database engine if no type is specified.

- **`BaseRepository(DatabaseType type)`**  
  Allows selection of either SQLite or SQL Server as the database engine.

### Properties

| Property                  | Type             | Description                                       |
|---------------------------|------------------|---------------------------------------------------|
| `DbProvider`              | `DbProviderType` | Choose your DbProvider (SQLite, SqlServer).       |
| `ConnectionString`        | `string`         | Connection string for the database.               |
| `ScriptsToExecute`        | `List<string>`   | List of SQL scripts to execute at initialization. |
| `DatabaseScriptsAssembly` | `Assembly`       | The assembly of the database scripts.             |
| `DatabaseScriptsLocation` | `string`         | The location of the database scripts.             |

### Enum: DatabaseType

Defines the database types supported by `BaseRepository`.

| Enum Value            | Description                             |
|-----------------------|-----------------------------------------|
| `SQLite`              | Uses SQLite as the database engine.     |
| `SQLServer`           | Uses SQL Server as the database engine. |

### Methods

#### Transactional Execution

- **`T ExecuteInTransaction(Func<IDbConnection, IDbTransaction, T> operation)`**  
  Executes a transactional operation and returns a result.

- **`void ExecuteInTransaction(Action<IDbConnection, IDbTransaction> operation)`**  
  Executes a transactional operation with no return value.

#### Query and Execution

- **`List<T> Query(string scriptName)`**  
  Executes a query script and returns a list of results.

- **`List<T> Query(string scriptName, object param)`**  
  Executes a parameterized query script and returns a list of results.

- **`T QuerySingleOrDefault(string scriptName, object param)`**  
  Executes a single-result query with parameters and returns the result.

- **`void Execute(string scriptName, object param)`**  
  Executes a script with parameters without returning a value.

- **`TResultType ExecuteScalar<TResultType>(string scriptName, object param)`**  
  Executes a script and returns a scalar result of specified type.

#### CRUD Operations

- **`T Get(int id)`**  
  Retrieves a single record by ID.

- **`List<T> GetAll()`**  
  Retrieves all records for the entity.

- **`List<T> GetWhere(object param)`**  
  Retrieves records that match the specified parameters.

- **`void Delete(int id)`**  
  Deletes a single record by ID.

- **`void DeleteWhere(object param)`**  
  Deletes records matching specified parameters.

### Example Usage

```csharp
using Mtf.Database;
using Mtf.Database.Models;
using System;
using System.Collections.Generic;

public class ExampleUsage
{
    public static void Main()
    {
        // Set up connection string and scripts
		BaseRepository.DbProvider = DbProviderType.SQLite;
        BaseRepository.ConnectionString = "Data Source=MyAppDatabase.db;Version=3;";
		BaseRepository.DatabaseScriptsAssembly = Assembly.GetEntryAssembly();
		BaseRepository.DatabaseScriptsLocation = "MyApp.Database";
        BaseRepository.ScriptsToExecute = new List<string> { "CreateDatabase", "Migration1" };

        // Use BaseRepository with SQL Server
        var serverRepository = new ServerRepository<Server>();

        // Insert and retrieve data
        var server = serverRepository.Get(1);
        Console.WriteLine($"Server ID: {server.Id}");

        // Delete data
        serverRepository.Delete(1);
        Console.WriteLine("Server deleted.");
    }
}
```

### Notes

- **Transactions**: `BaseRepository` automatically wraps operations in transactions for data consistency.
- **Script Management**: Define SQL scripts in `ScriptsToExecute` to automate setup tasks, such as creating tables.
- **Error Handling**: Wrap methods in `try-catch` blocks to manage exceptions during database interactions.