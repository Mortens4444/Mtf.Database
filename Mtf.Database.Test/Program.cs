using Mtf.Database;
using Mtf.Database.Attributes;
using Mtf.Database.Exceptions;
using Mtf.Database.Test;

// Column length validation runs before any connection is opened, so it needs no database.
var validationRepository = new ValidationRepository();
validationRepository.Validate("INSERT INTO Entries (Name) VALUES (@Name);", new LengthProbe { Name = "fits" });
try
{
    validationRepository.Validate("INSERT INTO Entries (Name) VALUES (@Name);", new LengthProbe { Name = "0123456789" });
    throw new Exception("Over-long value was not rejected");
}
catch (ValueTooLongException ex) when (ex.PropertyName == nameof(LengthProbe.Name) && ex.MaxLength == 5 && ex.ActualLength == 10)
{
    Console.WriteLine($"Length validation OK: {ex.Message}");
}
// A property the script does not reference is not validated.
validationRepository.Validate("UPDATE Entries SET Status = @Status WHERE Id = @Id;", new LengthProbe { Name = "0123456789" });

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

internal sealed class LengthProbe
{
    public long Id { get; set; }

    [Length(5)]
    public string? Name { get; set; }

    public string? Status { get; set; }
}

internal sealed class ValidationRepository() : BaseRepository("Data Source=unused")
{
    public void Validate(string sql, object param) => ValidateParameters(sql, param);
}
