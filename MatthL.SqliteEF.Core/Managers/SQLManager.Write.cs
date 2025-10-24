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
        private int _maximumWritingTime_ms = 10000;
        public int MaximumWritingTime_ms
        {
            get { return _maximumWritingTime_ms; }
            set { _maximumWritingTime_ms = value; }
        }

        public async Task<Result> AddAsync<T>(T entity) where T : class, IBaseEntity
        {
            if (entity == null)
                return Result.Failure("Entity is null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanCreate)
                    return Result.Failure($"Unauthorized to create {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                await context.Set<T>().AddAsync(entity);
                await context.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to add: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<Result> AddRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            if (entities == null)
                return Result.Failure("Entities are null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanCreate)
                    return Result.Failure($"Unauthorized to create {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                await context.Set<T>().AddRangeAsync(entities);
                await context.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to add range: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<Result> UpdateAsync<T>(T entity) where T : class, IBaseEntity
        {
            if (entity == null)
                return Result.Failure("Entity is null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanUpdate)
                    return Result.Failure($"Unauthorized to update {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                context.Set<T>().Update(entity);
                await context.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to update: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<Result> UpdateRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            if (entities == null)
                return Result.Failure("Entities are null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanUpdate)
                    return Result.Failure($"Unauthorized to update {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                context.Set<T>().UpdateRange(entities);
                await context.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to update range: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<Result> DeleteAsync<T>(T entity) where T : class, IBaseEntity
        {
            if (entity == null)
                return Result.Failure("Entity is null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanDelete)
                    return Result.Failure($"Unauthorized to delete {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                context.Set<T>().Remove(entity);
                await context.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to delete: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<Result> DeleteRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            if (entities == null)
                return Result.Failure("Entities are null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanDelete)
                    return Result.Failure($"Unauthorized to delete {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                context.Set<T>().RemoveRange(entities);
                await context.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to delete range: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<Result> DeleteAllAsync<T>() where T : class, IBaseEntity
        {
            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanDelete)
                    return Result.Failure($"Unauthorized to delete {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                var entities = await context.Set<T>().ToListAsync();
                context.Set<T>().RemoveRange(entities);
                await context.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to delete all: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<Result<List<T>>> GetAllAsync<T>() where T : class, IBaseEntity
        {
            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<List<T>>.Failure($"Unauthorized to read {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                var entities = await context.Set<T>().ToListAsync();
                return Result<List<T>>.Success(entities);
            }
            catch (Exception ex)
            {
                return Result<List<T>>.Failure($"Failed to get all: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        public async Task<Result<T>> GetByIdAsync<T>(int id) where T : class, IBaseEntity
        {
            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<T>.Failure($"Unauthorized to read {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                var entity = await context.Set<T>().FindAsync(id);
                if (entity == null)
                    return Result<T>.Failure($"Entity with ID {id} not found");

                return Result<T>.Success(entity);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure($"Failed to get by ID: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        public async Task<Result<bool>> AnyExistAsync<T>() where T : class, IBaseEntity
        {
            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<bool>.Failure($"Unauthorized to read {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);

                var exists = await context.Set<T>().AnyAsync();
                return Result<bool>.Success(exists);
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure($"Failed to check existence: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        public Result<bool> IsSavable<T>(T entity) where T : class, IBaseEntity
        {
            if (entity == null)
                return Result<bool>.Failure("Entity is null");

            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();

                // Si l'entité a un ID > 0, c'est une mise à jour, sinon c'est une création
                bool isUpdate = entity.Id > 0;
                bool canSave = isUpdate ? authorization.CanUpdate : authorization.CanCreate;

                return Result<bool>.Success(canSave);
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure($"Failed to check savability: {ex.Message}");
            }
        }

        public async Task<Result> AddOrUpdateAsync<T>(T entity) where T : class, IBaseEntity
        {
            if (entity == null)
                return Result.Failure("Entity is null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();

                using var context = _contextFactory(_databaseManager.FullPath);

                // Vérifier si l'entité existe déjà
                var existingEntity = await context.Set<T>().FindAsync(entity.Id);

                if (existingEntity != null)
                {
                    // Update
                    if (!authorization.CanUpdate)
                        return Result.Failure($"Unauthorized to update {typeof(T).Name}");

                    context.Entry(existingEntity).CurrentValues.SetValues(entity);
                }
                else
                {
                    // Add
                    if (!authorization.CanCreate)
                        return Result.Failure($"Unauthorized to create {typeof(T).Name}");

                    await context.Set<T>().AddAsync(entity);
                }

                await context.SaveChangesAsync();
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to add or update: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}
