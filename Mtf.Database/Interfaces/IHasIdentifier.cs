namespace Mtf.Database.Interfaces;

public interface IHasIdentifier<TIdentifierType>
{
    TIdentifierType Id { get; set; }
}
