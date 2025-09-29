using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using MatthL.SqliteEF.Core.Services;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using MatthL.SqliteEF.Core.Models;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.ResultLogger.Core.Models;

namespace MatthL.SqliteEF.Core.Services
{
    public class BaseService<T> : IService<T> where T : class, IBaseEntity
    {
        protected readonly DbContext _dbContext;
        protected readonly DbSet<T> _dbSet;
        protected readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);
        private IAuthorizationManager authorizationManager;

        public BaseService(DbContext dbContext, IAuthorizationManager _authorizationManager)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _dbSet = _dbContext.Set<T>();
            authorizationManager = _authorizationManager;
        }

        #region Core Database Operations

        protected virtual async Task<Result> SaveChangesAsync()
        {
            try
            {
                if (_dbContext == null) return Result.Failure("The DBContext is null");

                // Log des changements détectés
                var entries = _dbContext.ChangeTracker.Entries()
                    .Where(e => e.State != EntityState.Unchanged)
                    .ToList();

                var changes = await _dbContext.SaveChangesAsync();
                return Result.Success();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await HandleConcurrencyConflict(ex);
                return Result.Failure("SaveChangesAsync: Concurrency exception");
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result.Failure($"Failed to save changes: {ex.InnerException.Message}");
                return Result.Failure($"Failed to save changes: {ex.Message}");
            }
        }

        protected virtual async Task HandleConcurrencyConflict(DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                await entry.ReloadAsync();
            }
        }

        protected virtual string GetTableName()
        {
            var entityType = _dbContext.Model.FindEntityType(typeof(T));
            return entityType?.GetTableName() ?? typeof(T).Name;
        }

        protected virtual IQueryable<T> GetQueryWithNavigations()
        {
            return _dbContext.Set<T>().IncludeAllNavigations(_dbContext);
        }

        #endregion

        #region IService Implementation

        public async Task<Result> AddOrUpdateAsync(T entity)
        {
            try
            {
                if (entity == null) return Result.Failure("Entity is null");

                // Vérifier si l'entité existe déjà par son ID
                var exists = await _dbSet.AnyAsync(e => e.Id == entity.Id && entity.Id != 0);

                if (exists)
                {
                    return await UpdateAsync(entity);
                }
                else
                {
                    return await AddAsync(entity);
                }
            }
            catch(Exception ex)
            {
                return Result.Failure($"Failed to add {ex.Message}");
            }
        }

        public async Task<Result> AddAsync(T entity)
        {
            if (entity == null)
            {
                return Result.Failure("The entity is empty");
            }

            try
            {
                await _syncLock.WaitAsync();
                var tableName = GetTableName();
                var canCreate = authorizationManager.CanCreate(tableName);

                if (!canCreate)
                {
                    return Result.Failure($"Unauthorized to create in the table {tableName}");
                }

                var entryBefore = _dbContext.Entry(entity);
                _dbSet.Add(entity);
                var entryAfter = _dbContext.Entry(entity);
                var result = await SaveChangesAsync();

                return result;
            }
            catch (Exception ex)
            {
                if(ex.InnerException != null) return Result.Failure($"Failed to add entity: {ex.InnerException.Message}"); 
                return Result.Failure($"Failed to add entity: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<Result> AddListAsync(IEnumerable<T> entities)
        {
            if (entities == null || !entities.Any()) return Result.Failure("List is empty");

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanCreate(GetTableName()))
                {
                    return Result.Failure($"Unauthorized to create in the table {GetTableName()}");
                }

                _dbSet.AddRange(entities);
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result.Failure($"Failed to add entity List: {ex.InnerException.Message}");
                return Result.Failure($"Failed to add entity List: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<Result> UpdateAsync(T entity)
        {
            if (entity == null) return Result.Failure("the entity is null");

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanUpdate(GetTableName()))
                {
                    
                    return Result.Failure($"Unauthorized to update in the table {GetTableName()}");
                }

                var existingEntity = await _dbSet.FindAsync(entity.Id);
                if (existingEntity == null)
                {
                    return Result.Failure($"the entity is not existing {entity.Id}");
                }

                _dbContext.Entry(existingEntity).CurrentValues.SetValues(entity);
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result.Failure($"Failed to update entity: {ex.InnerException.Message}");
                return Result.Failure($"Failed to update entity: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<Result> UpdateListAsync(IEnumerable<T> entities)
        {
            if (entities == null || !entities.Any()) return Result.Failure("the entity list is null");

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanUpdate(GetTableName()))
                {
                    return Result.Failure($"Unauthorized to update the table {GetTableName()}");
                }

                foreach (var entity in entities)
                {
                    var existingEntity = await _dbSet.FindAsync(entity.Id);
                    if (existingEntity != null)
                    {
                        _dbContext.Entry(existingEntity).CurrentValues.SetValues(entity);
                    }
                }

                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result.Failure($"Failed to Update entity List: {ex.InnerException.Message}");
                return Result.Failure($"Failed to Update entity list: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<Result> DeleteAsync(T entity)
        {
            if (entity == null) return Result.Failure("the entity is null");

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanDelete(GetTableName()))
                {
                    return Result.Failure($"Unauthorized to delete in the table {GetTableName()}");
                }

                _dbSet.Remove(entity);
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result.Failure($"Failed to delete entity: {ex.InnerException.Message}");
                return Result.Failure($"Failed to delete entity: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<Result> DeleteListAsync(IEnumerable<T> entities)
        {
            if (entities == null || !entities.Any()) return Result.Failure("The List is empty or null");

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanDelete(GetTableName()))
                {
                    return Result.Failure($"Unauthorized to delete in the table {GetTableName()}");
                }

                _dbSet.RemoveRange(entities);
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result.Failure($"Failed to Delete entity: {ex.InnerException.Message}");
                return Result.Failure($"Failed to Delete entity: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<Result> DeleteAllAsync()
        {
            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanDelete(GetTableName()))
                {
                    return Result.Failure($"Unauthorized to delete in the table {GetTableName()}");
                }

                // Utilisation de ExecuteDeleteAsync pour une suppression efficace
                await _dbSet.ExecuteDeleteAsync();
                return Result.Success();
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result.Failure($"Failed to delete all : {ex.InnerException.Message}");
                return Result.Failure($"Failed to delete all : {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<Result<List<T>>> GetAllAsync()
        {
            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanRead(GetTableName()))
                {
                    return Result<List<T>>.Failure($"Unauthorized to read the table {GetTableName()}");
                }

                return Result<List<T>>.Success(await GetQueryWithNavigations().ToListAsync());
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result<List<T>>.Failure($"Failed to get all: {ex.InnerException.Message}");
                return Result<List<T>>.Failure($"Failed to get all: {ex.Message}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<Result<T>> GetItem(int id)
        {
            try
            {
                if (!authorizationManager.CanRead(GetTableName()))
                {
                    return Result<T>.Failure($"Unauthorized to read the table {GetTableName()}");
                }
                var item = await GetQueryWithNavigations().FirstOrDefaultAsync();
                if (item == null) return Result<T>.Failure($"the item {id} could not be found");
                return Result<T>.Success(item);
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result<T>.Failure($"Failed to get entity: {ex.InnerException.Message}");
                return Result<T>.Failure($"Failed to get entity: {ex.Message}");
            }
        }

        public async Task<Result<bool>> AnyExist()
        {
            try
            {
                return Result<bool>.Success(await _dbSet.AnyAsync());
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result<bool>.Failure($"Failed to discover entity: {ex.InnerException.Message}");
                return Result<bool>.Failure($"Failed to discover entity: {ex.Message}");
            }
        }

        public Result<bool> IsSavable(T entity)
        {
            if (entity == null) return Result<bool>.Failure("Empty entity is not savable");

            try
            {
                var model = _dbContext.Model;
                var entityType = model.FindEntityType(entity.GetType());

                foreach (var property in entityType.GetProperties())
                {
                    if (!property.IsNullable)
                    {
                        var entry = _dbContext.Entry(entity);
                        var value = entry.Property(property.Name).CurrentValue;

                        if (value == null || (value is string strValue && string.IsNullOrWhiteSpace(strValue)))
                        {
                            return Result<bool>.Success(false);
                        }
                    }
                }

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null) return Result<bool>.Failure($"Failed to verify entity savable: {ex.InnerException.Message}");
                return Result<bool>.Failure($"Failed to verify entity savable: {ex.Message}");
            }
        }

        #endregion
    }


}