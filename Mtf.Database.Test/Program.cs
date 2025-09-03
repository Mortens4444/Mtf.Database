using Mtf.Database;

BaseRepository.ConnectionString = @"Data Source=localhost\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;";
Console.WriteLine(BaseRepository.ExecuteScalarQuery("SELECT HOST_NAME()"));
Console.WriteLine(BaseRepository.ExecuteScalarQuery("SELECT SUSER_NAME()"));

BaseRepository.ConnectionString = @"Data Source=localhost\SQLEXPRESS;Initial Catalog=LiveView;Integrated Security=True;";
if (!BaseRepository.HasValidSqlSyntax("DELETE FROM Permissions WHERE GroupId = @GroupId;"))
{
    throw new Exception("Invalid SQL syntax");
}