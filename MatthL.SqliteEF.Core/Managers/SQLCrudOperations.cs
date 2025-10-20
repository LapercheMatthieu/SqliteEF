using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
[assembly: InternalsVisibleTo("SQLiteManager.Tests")]
namespace MatthL.SqliteEF.Core.Managers
{
    public partial class SQLManager
    {
        private readonly SQLManager _manager;
        private SemaphoreSlim SemaphoreWrite = new SemaphoreSlim(1, 1);
        private int _maximumWaitingTime = 1000;
        public int MaximumWaitingTime_ms
        {
            get { return _maximumWaitingTime; }
            set { _maximumWaitingTime = value; }
        }

        public async Task<Result> AddAsync<T>(T entity) where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot Add because the service was not found");
            try
            {
                await SemaphoreWrite.WaitAsync();
                return await result.Value.AddAsync(entity);
            }
            finally
            {
                SemaphoreWrite.Release();
            }
        }

        public async Task<Result> AddRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot Add List because the service was not found");
            try
            {
                await SemaphoreWrite.WaitAsync();
                return await result.Value.AddListAsync(entities);
            }
            finally
            {
                SemaphoreWrite.Release();
            }
            
        }

        public async Task<Result> UpdateAsync<T>(T entity) where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot update because the service was not found");
            try
            {
                await SemaphoreWrite.WaitAsync();
                return await result.Value.UpdateAsync(entity);
            }
            finally
            {
                SemaphoreWrite.Release();
            }
            
        }

        public async Task<Result> UpdateRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot update list because the service was not found");
            try
            {
                await SemaphoreWrite.WaitAsync();
                return await result.Value.UpdateListAsync(entities);
            }
            finally
            {
                SemaphoreWrite.Release();
            }
            
        }

        public async Task<Result> DeleteAsync<T>(T entity) where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot delete because the service was not found");
            try
            {
                await SemaphoreWrite.WaitAsync();
                return await result.Value.DeleteAsync(entity);
            }
            finally
            {
                SemaphoreWrite.Release();
            }
            
        }

        public async Task<Result> DeleteRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot delete list because the service was not found");
            try
            {
                await SemaphoreWrite.WaitAsync();
                return await result.Value.DeleteListAsync(entities);
            }
            finally
            {
                SemaphoreWrite.Release();
            }
            
        }

        public async Task<Result> DeleteAllAsync<T>() where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot delete all because the service was not found");
            try
            {
                await SemaphoreWrite.WaitAsync();
                return await result.Value.DeleteAllAsync();
            }
            finally
            {
                SemaphoreWrite.Release();
            }
            
        }

        public async Task<Result<List<T>>> GetAllAsync<T>() where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result<List<T>>.Failure("Cannot get all because the service was not found");

            return await result.Value.GetAllAsync();
        }

        public async Task<Result<T>> GetByIdAsync<T>(int id) where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result<T>.Failure("Cannot get by ID because the service was not found");

            return await result.Value.GetItem(id);
        }

        public async Task<Result<bool>> AnyExistAsync<T>() where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result<bool>.Failure("Cannot check existing because the service was not found");

            return await result.Value.AnyExist();
        }

        public Result<bool> IsSavable<T>(T entity) where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result<bool>.Failure("Cannot check savability because the service was not found");

            return result.Value.IsSavable(entity);
        }

        public async Task<Result> AddOrUpdateAsync<T>(T entity) where T : class, IBaseEntity
        {
            var result = GetService<T>();
            if (result.IsFailure)
                return Result.Failure("Cannot Add or update because the service was not found");

            return await result.Value.AddOrUpdateAsync(entity);
        }

        /// <summary>
        /// Research with custom filters
        /// </summary>
        public async Task<Result<List<T>>> WhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class, IBaseEntity
        {
            try
            {
                if (_dbContext == null)
                    return Result<List<T>>.Failure("DbContext is null");

                if (!IsConnected)
                    return Result<List<T>>.Failure("Not connected to database");

                var results = await _dbContext.Set<T>()
                    .Where(predicate)
                    .ToListAsync();

                _connectionManager.UpdateActivity();
                return Result<List<T>>.Success(results);
            }
            catch (Exception ex)
            {
                return Result<List<T>>.Failure($"Query failed: {ex.Message}");
            }
        }
    }
}
