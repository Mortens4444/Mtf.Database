using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mtf.Database.Interfaces;

public interface IBaseRepository<TEntity, TIdentifierType>
    where TEntity : class, IHasIdentifier<TIdentifierType>
{
    Task<List<TEntity>> GetAllAsync();

    Task<TEntity?> GetByIdAsync(TIdentifierType id);

    Task<TEntity?> InsertAsync(TEntity entity);

    Task<TEntity?> UpdateAsync(TEntity entity);

    Task DeleteAsync(TIdentifierType id);
}