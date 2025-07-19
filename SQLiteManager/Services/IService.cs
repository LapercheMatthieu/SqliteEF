using SQLiteManager.Models;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace SQLiteManager.Services
{
    public interface IService<T> where T : class, IBaseEntity
    {
        Task<bool> AddOrUpdateAsync(T entity);
        Task<bool> AddAsync(T entity);
        Task<bool> AddListAsync(IEnumerable<T> entities);
        Task<bool> UpdateAsync(T entity);
        Task<bool> UpdateListAsync(IEnumerable<T> entities);
        Task<bool> DeleteAsync(T entity);
        Task<bool> DeleteListAsync(IEnumerable<T> entities);
        Task<bool> DeleteAllAsync();
        Task<List<T>> GetAllAsync();
        Task<T> GetItem(int Id);

        Task<bool> AnyExist();
        bool IsSavable(T entity);
    }


}