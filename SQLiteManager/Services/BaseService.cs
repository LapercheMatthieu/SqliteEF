using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using SQLiteManager.Authorizations;
using SQLiteManager.Models;
using SQLiteManager.Services;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;

namespace SQLiteManager.Services
{
    public class BaseService<T> : IService<T> where T : class, IBaseEntity
    {
        protected readonly DbContext _dbContext;
        protected readonly DbSet<T> _dbSet;
        protected readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);
        private IAuthorizationManager authorizationManager;

        public event Action<string> OperationFailed;

        public BaseService(DbContext dbContext, IAuthorizationManager _authorizationManager)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _dbSet = _dbContext.Set<T>();
            authorizationManager = _authorizationManager;
        }

        protected virtual void RaiseOperationFailed(string message)
        {
            OperationFailed?.Invoke(message);
        }

        #region Core Database Operations

        protected virtual async Task<bool> SaveChangesAsync()
        {
            try
            {
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await HandleConcurrencyConflict(ex);
                return false;
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to save changes: {ex.Message}");
                return false;
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

        public async Task<bool> AddOrUpdateAsync(T entity)
        {
            if (entity == null) return false;

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

        public async Task<bool> AddAsync(T entity)
        {
            if (entity == null) return false;

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanCreate(GetTableName()))
                {
                    RaiseOperationFailed("Unauthorized create operation");
                    return false;
                }

                _dbSet.Add(entity);
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to add entity: {ex.Message}");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<bool> AddListAsync(IEnumerable<T> entities)
        {
            if (entities == null || !entities.Any()) return false;

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanCreate(GetTableName()))
                {
                    RaiseOperationFailed("Unauthorized create operation");
                    return false;
                }

                _dbSet.AddRange(entities);
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to add entities: {ex.Message}");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<bool> UpdateAsync(T entity)
        {
            if (entity == null) return false;

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanUpdate(GetTableName()))
                {
                    RaiseOperationFailed("Unauthorized update operation");
                    return false;
                }

                var existingEntity = await _dbSet.FindAsync(entity.Id);
                if (existingEntity == null)
                {
                    RaiseOperationFailed("Entity not found");
                    return false;
                }

                _dbContext.Entry(existingEntity).CurrentValues.SetValues(entity);
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to update entity: {ex.Message}");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<bool> UpdateListAsync(IEnumerable<T> entities)
        {
            if (entities == null || !entities.Any()) return false;

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanUpdate(GetTableName()))
                {
                    RaiseOperationFailed("Unauthorized update operation");
                    return false;
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
                RaiseOperationFailed($"Failed to update entities: {ex.Message}");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<bool> DeleteAsync(T entity)
        {
            if (entity == null) return false;

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanDelete(GetTableName()))
                {
                    RaiseOperationFailed("Unauthorized delete operation");
                    return false;
                }

                _dbSet.Remove(entity);
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to delete entity: {ex.Message}");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<bool> DeleteListAsync(IEnumerable<T> entities)
        {
            if (entities == null || !entities.Any()) return false;

            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanDelete(GetTableName()))
                {
                    RaiseOperationFailed("Unauthorized delete operation");
                    return false;
                }

                _dbSet.RemoveRange(entities);
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to delete entities: {ex.Message}");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<bool> DeleteAllAsync()
        {
            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanDelete(GetTableName()))
                {
                    RaiseOperationFailed("Unauthorized delete operation");
                    return false;
                }

                // Utilisation de ExecuteDeleteAsync pour une suppression efficace
                return await _dbSet.ExecuteDeleteAsync() > 0;
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to delete all entities: {ex.Message}");
                return false;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<List<T>> GetAllAsync()
        {
            try
            {
                await _syncLock.WaitAsync();

                if (!authorizationManager.CanRead(GetTableName()))
                {
                    RaiseOperationFailed("Unauthorized read operation");
                    return new List<T>();
                }

                return await GetQueryWithNavigations().ToListAsync();
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to get entities: {ex.Message}");
                return new List<T>();
            }
            finally
            {
                _syncLock.Release();
            }
        }

        public async Task<T> GetItem(int id)
        {
            try
            {
                if (!authorizationManager.CanRead(GetTableName()))
                {
                    RaiseOperationFailed("Unauthorized read operation");
                    return null;
                }

                return await GetQueryWithNavigations().FirstOrDefaultAsync(e => e.Id == id);
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to get entity: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> AnyExist()
        {
            try
            {
                return await _dbSet.AnyAsync();
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to check if any entity exists: {ex.Message}");
                return false;
            }
        }

        public bool IsSavable(T entity)
        {
            if (entity == null) return false;

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
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                RaiseOperationFailed($"Failed to check if entity is savable: {ex.Message}");
                return false;
            }
        }

        #endregion
    }


}