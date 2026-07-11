using System.Collections.ObjectModel;

namespace Mtf.Database.Interfaces;

public interface IRepository<TModelType, TIdentifierType>
{
    TModelType? Select(TIdentifierType id);

    ReadOnlyCollection<TModelType> SelectAll();

    ReadOnlyCollection<TModelType> SelectWhere(object param);

    void Insert(TModelType model);

    T InsertAndReturnId<T>(TModelType model) where T : struct;

    void Update(TModelType model);

    void Delete(TIdentifierType id);

    void DeleteWhere(object param);
}
