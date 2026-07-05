using System;
using System.Data.Common;

namespace Mtf.Database.Services;

public static class Utils
{
    public static string? GetDatabaseName(string connectionString)
    {
        if (String.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        if (builder.TryGetValue("Database", out var dbName))
        {
            return dbName.ToString();
        }

        if (builder.TryGetValue("Initial Catalog", out var initialCatalog))
        {
            return initialCatalog.ToString();
        }

        return null;
    }
}
