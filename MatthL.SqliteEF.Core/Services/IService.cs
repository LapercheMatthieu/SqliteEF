using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Models;

namespace MatthL.SqliteEF.Core.Services
{
    public interface IService<T> where T : class, IBaseEntity
    {
        Task<Result> AddOrUpdateAsync(T entity);
        Task<Result> AddAsync(T entity);
        Task<Result> AddListAsync(IEnumerable<T> entities);
        Task<Result> UpdateAsync(T entity);
        Task<Result> UpdateListAsync(IEnumerable<T> entities);
        Task<Result> DeleteAsync(T entity);
        Task<Result> DeleteListAsync(IEnumerable<T> entities);
        Task<Result> DeleteAllAsync();
        Task<Result<List<T>>> GetAllAsync();
        Task<Result<T>> GetItem(int Id);

        Task<Result<bool>> AnyExist();
        Result<bool> IsSavable(T entity);
    }


}