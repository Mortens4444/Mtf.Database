using Mtf.Database.Models;

namespace Mtf.Database.Test;

internal class LogRepository : BaseRepository<LogEntry, long>
{
    public LogRepository() : base(@"Data Source=localhost\SQLEXPRESS;Initial Catalog=KalmarLogs;Integrated Security=True;") { }
}
