# Mtf.Database BaseRepository Documentation

## Overview

The `Mtf.Database` library provides a robust, extensible data access layer based on **Dapper**. It features an abstract `BaseRepository` and a generic `BaseRepository<TEntity, TIdentifierType>` class designed to manage CRUD operations, automatic transactions, raw query executions, syntax validations, and structured database migrations. It supports both **SQLite** and **SQL Server**, dynamically switching providers at runtime.

---

## Installation

To use `Mtf.Database` in your project:

1. **Add Package**:
```bash
dotnet add package Mtf.Database

```


2. **Include the Namespace**:
```csharp
using Mtf.Database;

```

---

## Class: `BaseRepository`

The non-generic base class handles core connection configurations, direct script executions, custom raw queries, syntax parsing, and transactional fallbacks.

### Static and Instance Properties

| Property | Type | Scope | Description |
| --- | --- | --- | --- |
| `ConnectionString` | `string` | Instance | The database connection string (init-only). |
| `CommandTimeout` | `int?` | Static | Optional global command timeout setting in seconds. |
| `DbProvider` | `DbProviderType` | Static | Specifies the active engine (`SQLite` or `SqlServer`). Default is `SqlServer`. |
| `ScriptsToExecute` | `List<string>` | Static | Pre-registered database script names intended for migrations. |
| `DatabaseScriptsAssembly` | `Assembly?` | Static | Assembly containing embedded SQL script resources. |
| `DatabaseScriptsLocation` | `string?` | Static | Root namespace path to embedded SQL scripts. |

---

### Key Methods

#### Migration Management

* **`static void ExecuteMigrations(string connectionString)`**
Iterates through `ScriptsToExecute`, fetches script contents via `ScriptCache`, and applies them inside a single database transaction. Rollback occurs automatically on failure, throwing a `SqlScriptExecutionException`.

#### Synchronous & Asynchronous Script Execution

* **`void Execute(string scriptName, object? param = null)`**
Executes an embedded SQL script wrapped in an isolated transaction.
* **`Task ExecuteAsync(string scriptName, object? param = null)`**
Asynchronous variant of `Execute` using structural async transaction handlers.
* **`void ExecuteWithoutTransaction(string scriptName, object? param = null)`**
Runs a script outside of an active database transaction block.
* **`void Execute(params SqlParam[] parameters)`**
Batch executes multiple `SqlParam` entries sequentially within a single unified transaction.

#### Raw Queries and Metrics

* **`DataTable ExecuteQuery(string query, Dictionary<string, object>? parameters = null)`**
Executes a raw SQL statement, passing structured query parameters safely. Returns a standard `DataTable`. Throws an `ArgumentException` if the input text contains potentially destructive keywords.
* **`string? ExecuteScalarQuery(string query)`**
Quickly evaluates a query and returns its scalar output as a string.
* **`int GetDatabaseUsagePercentageWithLimit()`**
*(SQL Server exclusive)* Returns the total storage consumption percentage relative to engine limits (e.g., managing the 10GB boundary in Express editions).

#### SQL Parsing and Validation

* **`bool HasValidSqlSyntax(string sql, bool checkDeclarations, out Exception? exception)`**
Validates standard database script structures. Internally leverages `SET PARSEONLY ON` across processing batches without physical script commitment.

#### Parameters and Column Length Validation

Parameter objects are handed to Dapper as they are, so everything Dapper binds works: plain objects and anonymous types (only the properties the script references are bound), `Dictionary<string, object>`, `DynamicParameters`, and collections for multi-exec (the script runs once per element).

A string property can declare the maximum length of its column with `[Length(n)]` from `Mtf.Database.Attributes` (which derives from `MaxLengthAttribute`), or with the BCL `[MaxLength(n)]`, `[StringLength(n)]` or `[Length(min, max)]` from `System.ComponentModel.DataAnnotations`. Before a script, stored procedure or `InsertAndReturnId` runs, the repository checks every annotated string property the command binds and throws `ValueTooLongException` (an `ArgumentException` carrying `TypeName`, `PropertyName`, `MaxLength` and `ActualLength`) when a value is longer than declared. The check runs before any connection is opened and is never wrapped in `SqlScriptExecutionException`. Trailing spaces beyond the limit are ignored, as SQL Server ignores them. Values are never truncated client-side: without a declared length an over-long value is rejected by the database itself (SQL Server error 2628), or stored in full on SQLite, which has no column lengths.

```csharp
public class ServerEntity : IHasIdentifier<int>
{
    public int Id { get; set; }

    [Length(50)]
    public string Name { get; set; } = string.Empty;
}

try
{
    repo.Insert(new ServerEntity { Name = new string('x', 60) });
}
catch (ValueTooLongException ex)
{
    // ex.PropertyName == "Name", ex.MaxLength == 50, ex.ActualLength == 60
}
```

Only the properties a text script references are validated, so an over-long value in a column the script does not write does not block the script. Stored procedures bind every property, so every annotated property is validated for them. Subclasses calling the protected `ExecuteInternal` directly can run the same check with the protected `ValidateParameters(sql, param, commandType)`.

---

## Class: `BaseRepository<TEntity, TIdentifierType>`

An abstract generic repository enforcing strongly-typed lifecycle workflows for internal business entities.

```csharp
public abstract class BaseRepository<TEntity, TIdentifierType> : BaseRepository 
    where TEntity : class, IHasIdentifier<TIdentifierType>

```

### Script Naming Convention

The repository automatically resolves operational script pathways at instantiation. It expects scripts to reside inside an embedded directory mirroring the structural layout: `{ScriptsSubfolderName}.{Operation}{EntityName}` (e.g., `Server.SelectServer`).

---

### Contextual Operations

#### Custom Target Transactions

* **`TEntity ExecuteInTransaction(Func<DbConnection, IDbTransaction, TEntity> operation)`**
Encapsulates explicit operations inside custom processing scopes returning unified state elements.
* **`void ExecuteInTransaction(Action<DbConnection, IDbTransaction> operation)`**
Encapsulates structural non-query operations inside structured workflows.

#### Direct Queries & Stored Procedures

* **`ReadOnlyCollection<TEntity> Query(string scriptName, object param)`** / **`QueryAsync(...)`**
Fetches relational datasets translated into read-only structural collections.
* **`TEntity? QuerySingleOrDefault(string scriptName, object? param = null)`**
Returns a unique domain record matching arguments or falls back to `null`.
* **`ReadOnlyCollection<TEntity> ExecuteStoredProcedure(string procedureName, object? param = null)`**
Invokes concrete procedural components returning mapped entities.

---

### Native CRUD Operations

| Method | Return Type | Strategy |
| --- | --- | --- |
| `Select(TIdentifierType id)` | `TEntity?` | Synch Lookup |
| `SelectAll()` | `ReadOnlyCollection<TEntity>` | Complete Fetch |
| `SelectWhere(object param)` | `ReadOnlyCollection<TEntity>` | Conditional Query |
| `Insert(TEntity? model)` | `void` | Standard Persistence |
| `InsertAndReturnId<T>(TEntity model)` | `T` (struct) | Evaluates context via `SCOPE_IDENTITY()` |
| `Update(TEntity? model)` | `void` | Structural Modification |
| `Delete(TIdentifierType id)` | `void` | Unique Record Erasure |
| `DeleteWhere(object? param)` | `void` | Conditional Mass Erasure |

#### Asynchronous Lifecycle Equivalents

* **`Task<ReadOnlyCollection<TEntity>> GetAllAsync()`**
* **`Task<ReadOnlyCollection<TEntity>> GetAllWhereAsync(object param)`**
* **`Task<TEntity?> GetByIdAsync(TIdentifierType id)`**
* **`Task<TEntity?> InsertAsync(TEntity entity)`** (Persists and returns the freshly loaded record)
* **`Task<TEntity?> UpdateAsync(TEntity entity)`** (Updates and returns the current database state)
* **`Task DeleteAsync(TIdentifierType id)`**

---

## Example Usage

```csharp
using Mtf.Database;
using Mtf.Database.Enums;
using System;
using System.Threading.Tasks;

public class ServerEntity : IHasIdentifier<int>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class ServerRepository : BaseRepository<ServerEntity, int>
{
    public ServerRepository(string connectionString) : base(connectionString) { }
}

public class Program
{
    public static async Task Main()
    {
        // 1. Global Setup Configuration
        BaseRepository.DbProvider = DbProviderType.SqlServer;
        BaseRepository.DatabaseScriptsAssembly = typeof(Program).Assembly;
        BaseRepository.DatabaseScriptsLocation = "YourApp.Database.Scripts";
        BaseRepository.CommandTimeout = 45;

        // Register Migrations
        BaseRepository.ScriptsToExecute.Add("Migration_v1_CreateTables");
        
        string connString = "Server=localhost;Database=LiveDB;Trusted_Connection=True;TrustServerCertificate=True;";
        
        // Run Database migrations
        BaseRepository.ExecuteMigrations(connString);

        // 2. Initialize instance repository
        var repo = new ServerRepository(connString);

        // 3. Perform Operations Asynchronously
        var newServer = new ServerEntity { Name = "Node-Alpha", Location = "Budapest" };
        ServerEntity? savedServer = await repo.InsertAsync(newServer);
        
        if (savedServer != null)
        {
            Console.WriteLine($"Successfully saved entity with ID: {savedServer.Id}");
        }

        // 4. Fetch structural lists conditionally
        var localServers = await repo.GetAllWhereAsync(new { Location = "Budapest" });
        foreach (var server in localServers)
        {
            Console.WriteLine($"Active Node: {server.Name}");
        }
    }
}

```