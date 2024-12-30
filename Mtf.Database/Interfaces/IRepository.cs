using System.Collections.ObjectModel;

namespace Mtf.Database.Interfaces
{
    public interface IRepository<TModelType>
    {
        TModelType Select(long id);

        TModelType Select(int id);

        ReadOnlyCollection<TModelType> SelectAll();

        ReadOnlyCollection<TModelType> SelectWhere(object param);

        void Insert(TModelType model);

        T InsertAndReturnId<T>(TModelType model) where T : struct;

        void Update(TModelType model);

        void Delete(int id);

        void Delete(long id);

        void DeleteWhere(object param);
    }
}
