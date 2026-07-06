using Mtf.Database;
using Mtf.Database.Test;

var masterRepository = new BaseRepository(@"Data Source=localhost\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;");
Console.WriteLine(masterRepository.ExecuteScalarQuery("SELECT HOST_NAME()"));
Console.WriteLine(masterRepository.ExecuteScalarQuery("SELECT SUSER_NAME()"));

var logRepository = new LogRepository();
if (!logRepository.HasValidSqlSyntax("DELETE FROM Permissions WHERE GroupId = @GroupId;", false, out var _))
{
    throw new Exception("Invalid SQL syntax");
}
if (!logRepository.HasValidSqlSyntax("DELETE FROM Agents WHERE VideoSourceId = @VideoSourceId;", false, out var __))
{
    throw new Exception("Invalid SQL syntax");
}

