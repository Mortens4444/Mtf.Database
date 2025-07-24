using System;

namespace Mtf.Database.Exceptions
{
    public class SqlScriptExecutionException : Exception
    {
        public String ScriptName { get; set; }

        public SqlScriptExecutionException() { }

        public SqlScriptExecutionException(string message) : base(message)
        {
        }

        public SqlScriptExecutionException(string scriptName, Exception innerException) : base($"Unable to execute script: {scriptName}", innerException)
        {
            ScriptName = scriptName;
        }
    }
}
