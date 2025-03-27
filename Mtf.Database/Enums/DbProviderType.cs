namespace Mtf.Database.Enums
{
    public enum DbProviderType
    {
#if !NET452
        SQLite,
#endif
        SqlServer
    }
}
