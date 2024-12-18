using Mtf.Database;

BaseRepository.ConnectionString = @"Data Source=localhost\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;";
Console.WriteLine(BaseRepository.ExecuteScalarQuery("SELECT HOST_NAME()"));
Console.WriteLine(BaseRepository.ExecuteScalarQuery("SELECT SUSER_NAME()"));