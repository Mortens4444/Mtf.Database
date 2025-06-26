using System.Collections.Generic;

namespace Mtf.Database.Interfaces
{
    public interface IRepositoryWithCompositeKey<TModel, TKey>
    {
        IEnumerable<TModel> SelectAll();

        TModel SelectByKey(TKey key);

        void Insert(TModel model);

        void Update(TModel model);

        void DeleteByKey(TKey key);
    }
}
