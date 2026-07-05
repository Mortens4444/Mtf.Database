using System;

namespace Mtf.Database.Exceptions;

public class SqlScriptExecutionException : Exception
{
    public string? DatabaseName { get; set; }

    public string ScriptName { get; set; } = String.Empty;

    public SqlScriptExecutionException() { }

    public SqlScriptExecutionException(string message) : base(message)
    {
    }

    public SqlScriptExecutionException(string? dbName, string scriptName, Exception innerException) : base($"Unable to execute script: {scriptName}", innerException)
    {
        DatabaseName = dbName;
        ScriptName = scriptName;
    }
}
