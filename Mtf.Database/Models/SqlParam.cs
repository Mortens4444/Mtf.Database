namespace Mtf.Database.Models;

public class SqlParam
{
    public required string ScriptName { get; set; }

    public object? Param { get; set; }
}
