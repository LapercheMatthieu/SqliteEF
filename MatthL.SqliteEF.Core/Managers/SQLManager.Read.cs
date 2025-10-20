using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Managers
{
    public partial class SQLManager
    {
        /// <summary>
        /// Research with custom filters
        /// </summary>
        public async Task<Result<List<T>>> WhereAsync<T>(Expression<Func<T, bool>> predicate) where T : class, IBaseEntity
        {
            if (predicate == null)
                return Result<List<T>>.Failure("Predicate is null");

            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<List<T>>.Failure($"Unauthorized to read {typeof(T).Name}");

                await using var context = await _contextFactory.CreateDbContextAsync();

                var results = await context.Set<T>()
                    .Where(predicate)
                    .ToListAsync();

                return Result<List<T>>.Success(results);
            }
            catch (Exception ex)
            {
                return Result<List<T>>.Failure($"Query failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        /// <summary>
        /// Verify existence with a filter
        /// </summary>
        public async Task<Result<bool>> AnyAsync<T>(Expression<Func<T, bool>> predicate) where T : class, IBaseEntity
        {
            if (predicate == null)
                return Result<bool>.Failure("Predicate is null");

            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<bool>.Failure($"Unauthorized to read {typeof(T).Name}");

                await using var context = await _contextFactory.CreateDbContextAsync();

                var exists = await context.Set<T>()
                    .AnyAsync(predicate);

                return Result<bool>.Success(exists);
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure($"Query failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        /// <summary>
        /// Paged query
        /// </summary>
        public async Task<Result<List<T>>> GetPagedAsync<T>(
            int pageNumber,
            int pageSize,
            Expression<Func<T, bool>> predicate = null,
            Expression<Func<T, object>> orderBy = null,
            bool ascending = true) where T : class, IBaseEntity
        {
            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<List<T>>.Failure($"Unauthorized to read {typeof(T).Name}");

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 10;

                await using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Set<T>().AsQueryable();

                if (predicate != null)
                    query = query.Where(predicate);

                if (orderBy != null)
                {
                    query = ascending
                        ? query.OrderBy(orderBy)
                        : query.OrderByDescending(orderBy);
                }

                var results = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Result<List<T>>.Success(results);
            }
            catch (Exception ex)
            {
                return Result<List<T>>.Failure($"Paged query failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        /// <summary>
        /// Get number of elements with a filter
        /// </summary>
        public async Task<Result<int>> CountAsync<T>(Expression<Func<T, bool>> predicate = null) where T : class, IBaseEntity
        {
            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<int>.Failure($"Unauthorized to read {typeof(T).Name}");

                await using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Set<T>().AsQueryable();

                if (predicate != null)
                    query = query.Where(predicate);

                var count = await query.CountAsync();

                return Result<int>.Success(count);
            }
            catch (Exception ex)
            {
                return Result<int>.Failure($"Count query failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        /// <summary>
        /// Get the first element matching a filter
        /// </summary>
        public async Task<Result<T>> FirstOrDefaultAsync<T>(Expression<Func<T, bool>> predicate) where T : class, IBaseEntity
        {
            if (predicate == null)
                return Result<T>.Failure("Predicate is null");

            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<T>.Failure($"Unauthorized to read {typeof(T).Name}");

                await using var context = await _contextFactory.CreateDbContextAsync();

                var result = await context.Set<T>()
                    .FirstOrDefaultAsync(predicate);

                if (result == null)
                    return Result<T>.Failure("No entity found matching the criteria");

                return Result<T>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure($"Query failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        /// <summary>
        /// Execute a custom query with a query builder pattern
        /// Usage: await ExecuteQueryAsync&lt;MyEntity&gt;(query => query.Where(x => x.Name == "test").OrderBy(x => x.Id))
        /// </summary>
        public async Task<Result<List<T>>> ExecuteQueryAsync<T>(
            Func<IQueryable<T>, IQueryable<T>> queryBuilder) where T : class, IBaseEntity
        {
            if (queryBuilder == null)
                return Result<List<T>>.Failure("Query builder is null");

            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<List<T>>.Failure($"Unauthorized to read {typeof(T).Name}");

                await using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Set<T>().AsQueryable();
                query = queryBuilder(query);

                var results = await query.ToListAsync();

                return Result<List<T>>.Success(results);
            }
            catch (Exception ex)
            {
                return Result<List<T>>.Failure($"Query execution failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        /// <summary>
        /// Execute a custom query with a query builder pattern (no tracking for read-only scenarios)
        /// Usage: await ExecuteQueryNoTrackingAsync&lt;MyEntity&gt;(query => query.Where(x => x.Name == "test").OrderBy(x => x.Id))
        /// </summary>
        public async Task<Result<List<T>>> ExecuteQueryNoTrackingAsync<T>(
            Func<IQueryable<T>, IQueryable<T>> queryBuilder) where T : class, IBaseEntity
        {
            if (queryBuilder == null)
                return Result<List<T>>.Failure("Query builder is null");

            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<List<T>>.Failure($"Unauthorized to read {typeof(T).Name}");

                await using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Set<T>().AsNoTracking().AsQueryable();
                query = queryBuilder(query);

                var results = await query.ToListAsync();

                return Result<List<T>>.Success(results);
            }
            catch (Exception ex)
            {
                return Result<List<T>>.Failure($"Query execution failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        /// <summary>
        /// Execute a custom single result query
        /// Usage: await ExecuteSingleQueryAsync&lt;MyEntity&gt;(query => query.Where(x => x.Id == 5).Include(x => x.RelatedEntity))
        /// </summary>
        public async Task<Result<T>> ExecuteSingleQueryAsync<T>(
            Func<IQueryable<T>, IQueryable<T>> queryBuilder) where T : class, IBaseEntity
        {
            if (queryBuilder == null)
                return Result<T>.Failure("Query builder is null");

            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<T>.Failure($"Unauthorized to read {typeof(T).Name}");

                await using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Set<T>().AsQueryable();
                query = queryBuilder(query);

                var result = await query.FirstOrDefaultAsync();

                if (result == null)
                    return Result<T>.Failure("No entity found");

                return Result<T>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure($"Query execution failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        /// <summary>
        /// Execute a custom aggregate query
        /// Usage: await ExecuteAggregateAsync&lt;MyEntity, decimal&gt;(query => query.Where(x => x.Active).Select(x => x.Price).SumAsync())
        /// </summary>
        public async Task<Result<TResult>> ExecuteAggregateAsync<T, TResult>(
            Func<IQueryable<T>, Task<TResult>> aggregateOperation) where T : class, IBaseEntity
        {
            if (aggregateOperation == null)
                return Result<TResult>.Failure("Aggregate operation is null");

            await _readLock.WaitAsync();
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanRead)
                    return Result<TResult>.Failure($"Unauthorized to read {typeof(T).Name}");

                await using var context = await _contextFactory.CreateDbContextAsync();

                var query = context.Set<T>().AsQueryable();
                var result = await aggregateOperation(query);

                return Result<TResult>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<TResult>.Failure($"Aggregate query failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }
    }
}
