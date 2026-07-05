namespace Mtf.Database.Test;

internal class MasterRepository : BaseRepository
{
    public MasterRepository() : base(@"Data Source=localhost\SQLEXPRESS;Initial Catalog=master;Integrated Security=True;") { }
}
