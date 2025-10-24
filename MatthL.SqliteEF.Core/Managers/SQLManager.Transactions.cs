using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Managers
{
    public partial class SQLManager
    {
        /// <summary>
        /// Execute operations in a transaction with proper context lifecycle management
        /// Usage: await ExecuteInTransactionAsync(async context => 
        /// {
        ///     context.Set&lt;MyEntity&gt;().Add(entity1);
        ///     context.Set&lt;MyEntity&gt;().Add(entity2);
        ///     await context.SaveChangesAsync();
        /// });
        /// </summary>
        public async Task<Result> ExecuteInTransactionAsync(Func<RootDbContext, Task> operations)
        {
            if (operations == null)
                return Result.Failure("Operations delegate is null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                using var context = _contextFactory(_databaseManager.FullPath);
                await using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    await operations(context);
                    await transaction.CommitAsync();

                    return Result.Success("Transaction completed successfully");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure($"Transaction rolled back: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return Result.Failure($"Transaction initialization failed: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Execute operations in a transaction with a result
        /// Usage: var result = await ExecuteInTransactionAsync(async context => 
        /// {
        ///     var entity = new MyEntity { Name = "Test" };
        ///     context.Set&lt;MyEntity&gt;().Add(entity);
        ///     await context.SaveChangesAsync();
        ///     return entity.Id;
        /// });
        /// </summary>
        public async Task<Result<TResult>> ExecuteInTransactionAsync<TResult>(
            Func<RootDbContext, Task<TResult>> operation)
        {
            if (operation == null)
                return Result<TResult>.Failure("Operation delegate is null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                using var context = _contextFactory(_databaseManager.FullPath);
                await using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    var result = await operation(context);
                    await transaction.CommitAsync();

                    return Result<TResult>.Success(result);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Result<TResult>.Failure($"Transaction rolled back: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return Result<TResult>.Failure($"Transaction initialization failed: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Execute multiple write operations in a single transaction with authorization checks
        /// Usage: await ExecuteBatchInTransactionAsync&lt;MyEntity&gt;(async (context, set) => 
        /// {
        ///     set.Add(entity1);
        ///     set.Add(entity2);
        ///     await context.SaveChangesAsync();
        /// });
        /// </summary>
        public async Task<Result> ExecuteBatchInTransactionAsync<T>(
            Func<RootDbContext, DbSet<T>, Task> batchOperations) where T : class, IBaseEntity
        {
            if (batchOperations == null)
                return Result.Failure("Batch operations delegate is null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                var authorization = _authorizationManager.GetAuthorization<T>();
                if (!authorization.CanCreate && !authorization.CanUpdate && !authorization.CanDelete)
                    return Result.Failure($"Unauthorized to perform write operations on {typeof(T).Name}");

                using var context = _contextFactory(_databaseManager.FullPath);
                await using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    await batchOperations(context, context.Set<T>());
                    await transaction.CommitAsync();

                    return Result.Success("Batch transaction completed successfully");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure($"Batch transaction rolled back: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return Result.Failure($"Batch transaction initialization failed: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Execute a read-only transaction (useful for ensuring consistency across multiple reads)
        /// Usage: await ExecuteReadTransactionAsync(async context => 
        /// {
        ///     var users = await context.Set&lt;User&gt;().ToListAsync();
        ///     var orders = await context.Set&lt;Order&gt;().Where(o => users.Select(u => u.Id).Contains(o.UserId)).ToListAsync();
        ///     return new { Users = users, Orders = orders };
        /// });
        /// </summary>
        public async Task<Result<TResult>> ExecuteReadTransactionAsync<TResult>(
            Func<RootDbContext, Task<TResult>> readOperation)
        {
            if (readOperation == null)
                return Result<TResult>.Failure("Read operation delegate is null");

            await _readLock.WaitAsync();
            try
            {
                using var context = _contextFactory(_databaseManager.FullPath);
                await using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    var result = await readOperation(context);
                    await transaction.CommitAsync(); // Commit even for reads to clean up

                    return Result<TResult>.Success(result);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Result<TResult>.Failure($"Read transaction failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return Result<TResult>.Failure($"Read transaction initialization failed: {ex.Message}");
            }
            finally
            {
                _readLock.Release();
            }
        }

        /// <summary>
        /// Execute a complex transaction with multiple entity types
        /// Usage: await ExecuteComplexTransactionAsync(async context => 
        /// {
        ///     var user = new User { Name = "John" };
        ///     context.Set&lt;User&gt;().Add(user);
        ///     await context.SaveChangesAsync();
        ///     
        ///     var order = new Order { UserId = user.Id };
        ///     context.Set&lt;Order&gt;().Add(order);
        ///     await context.SaveChangesAsync();
        ///     
        ///     return new { UserId = user.Id, OrderId = order.Id };
        /// });
        /// </summary>
        public async Task<Result<TResult>> ExecuteComplexTransactionAsync<TResult>(
            Func<RootDbContext, Task<TResult>> complexOperation,
            params Type[] entityTypes)
        {
            if (complexOperation == null)
                return Result<TResult>.Failure("Complex operation delegate is null");

            await _writeLock.WaitAsync(_maximumWritingTime_ms);
            try
            {
                // Check authorizations for all entity types involved
                if (entityTypes != null && entityTypes.Length > 0)
                {
                    foreach (var entityType in entityTypes)
                    {
                        var authMethod = _authorizationManager.GetType()
                            .GetMethod(nameof(_authorizationManager.GetAuthorization))
                            ?.MakeGenericMethod(entityType);

                        if (authMethod != null)
                        {
                            var auth = authMethod.Invoke(_authorizationManager, null);
                            var canCreate = (bool)auth.GetType().GetProperty("CanCreate")?.GetValue(auth);
                            var canUpdate = (bool)auth.GetType().GetProperty("CanUpdate")?.GetValue(auth);

                            if (!canCreate && !canUpdate)
                                return Result<TResult>.Failure($"Unauthorized to perform operations on {entityType.Name}");
                        }
                    }
                }

                using var context = _contextFactory(_databaseManager.FullPath);
                await using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    var result = await complexOperation(context);
                    await transaction.CommitAsync();

                    return Result<TResult>.Success(result);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Result<TResult>.Failure($"Complex transaction rolled back: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                return Result<TResult>.Failure($"Complex transaction initialization failed: {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}
