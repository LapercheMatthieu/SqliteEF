using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Models;
using MatthL.SqliteEF.Core.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
[assembly: InternalsVisibleTo("SQLiteManager.Tests")]
namespace MatthL.SqliteEF.Core.Managers.Delegates
{
    internal class SQLCrudOperations
    {
        private readonly SQLManager _manager;

        public SQLCrudOperations(SQLManager manager)
        {
            _manager = manager;
        }

        public async Task<Result> AddAsync<T>(T entity) where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot Add because the service was not found");

            return await result.Value.AddAsync(entity);
        }

        public async Task<Result> AddRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot Add List because the service was not found");

            return await result.Value.AddListAsync(entities);
        }

        public async Task<Result> UpdateAsync<T>(T entity) where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot update because the service was not found");

            return await result.Value.UpdateAsync(entity);
        }

        public async Task<Result> UpdateRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot update list because the service was not found");

            return await result.Value.UpdateListAsync(entities);
        }

        public async Task<Result> DeleteAsync<T>(T entity) where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot delete because the service was not found");

            return await result.Value.DeleteAsync(entity);
        }

        public async Task<Result> DeleteRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot delete list because the service was not found");

            return await result.Value.DeleteListAsync(entities);
        }

        public async Task<Result> DeleteAllAsync<T>() where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot delete all because the service was not found");

            return await result.Value.DeleteAllAsync();
        }

        public async Task<Result<List<T>>> GetAllAsync<T>() where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result<List<T>>.Failure("Cannot get all because the service was not found");

            return await result.Value.GetAllAsync();
        }

        public async Task<Result<T>> GetByIdAsync<T>(int id) where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result<T>.Failure("Cannot get by ID because the service was not found");

            return await result.Value.GetItem(id);
        }

        public async Task<Result<bool>> AnyExistAsync<T>() where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result<bool>.Failure("Cannot check existing because the service was not found");

            return await result.Value.AnyExist();
        }

        public Result<bool> IsSavable<T>(T entity) where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result<bool>.Failure("Cannot check savability because the service was not found");

            return result.Value.IsSavable(entity);
        }

        public async Task<Result> AddOrUpdateAsync<T>(T entity) where T : class, IBaseEntity
        {
            var result = _manager.GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot Add or update because the service was not found");

            return await result.Value.AddOrUpdateAsync(entity);
        }


    }
}
