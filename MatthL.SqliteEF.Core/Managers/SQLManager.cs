using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Core.Models;
using MatthL.SqliteEF.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace MatthL.SqliteEF.Core.Managers
{
    public partial class SQLManager
    {
        private readonly ConcurrentDictionary<Type, object> _services;
        private RootDbContext _dbContext;
        private readonly Func<RootDbContext> _dbContextbuilder;
        private readonly IAuthorizationManager _authorizationManager;

        // Gestionnaires délégués
        private readonly SQLConnectionManager _connectionManager;
        private readonly SQLDatabaseManager _databaseManager;
        private readonly SQLHealthChecker _healthChecker;

        public SQLConnectionManager ConnectionManager => _connectionManager;
        /// <summary>
        /// Access to the db context to by pass the functions, use it carefully
        /// </summary>
        public RootDbContext DbContext => _dbContext;

        // Propriétés déléguées pour l'état de connexion
        public ConnectionState CurrentState => _connectionManager.CurrentState;
        public bool IsConnected => _connectionManager.IsConnected;
        public DateTime? LastActivity => _connectionManager.LastActivityTime;
        // Événement relayé
        public event EventHandler<ConnectionState> ConnectionStateChanged
        {
            add => _connectionManager.ConnectionStateChanged += value;
            remove => _connectionManager.ConnectionStateChanged -= value;
        }
        public SQLManager(Func<RootDbContext> dbContextbuilder, string folderPath, string fileName, IAuthorizationManager authorizationManager = null)
        {
            _dbContextbuilder = dbContextbuilder;
            _dbContext = dbContextbuilder.Invoke();
            if (authorizationManager == null) authorizationManager = new AdminAuthorization();
            _authorizationManager = authorizationManager;
            _services = new ConcurrentDictionary<Type, object>();

            // Initialiser les gestionnaires
            _connectionManager = new SQLConnectionManager(_dbContext);
            _databaseManager = new SQLDatabaseManager(_dbContext, _connectionManager, folderPath, fileName );
            _healthChecker = new SQLHealthChecker(_connectionManager);

            _databaseManager.SetPaths(folderPath, fileName);
        }
        public SQLManager(Func<RootDbContext> dbContextbuilder)
        {
            _dbContext = dbContextbuilder.Invoke();
            _dbContextbuilder = dbContextbuilder;
            _authorizationManager = new AdminAuthorization();
            _services = new ConcurrentDictionary<Type, object>();

            // Initialiser les gestionnaires pour in-memory
            _connectionManager = new SQLConnectionManager(_dbContext);
            _databaseManager = new SQLDatabaseManager(_dbContext, _connectionManager); // Sans paths = in-memory
            _healthChecker = new SQLHealthChecker(_connectionManager);
        }
        // Propriétés publiques
        public string GetFullPath => _databaseManager.FullPath;
        public string GetFolderPath => _databaseManager.FolderPath;
        public string GetFileName => _databaseManager.FileName;
        public long GetFileSize => _databaseManager.FileSize;

        // Service management (reste dans SQLManager car c'est le cœur)
        public Result<IService<T>> GetService<T>() where T : class, IBaseEntity
        {
            var entityType = typeof(T);

            /* if (!_services.TryGetValue(entityType, out var service))
             {
                 service = new BaseService<T>(_dbContextbuilder.Invoke(), _authorizationManager);
                 _services[entityType] = service;
             }*/
            var service = new BaseService<T>(_dbContextbuilder.Invoke(), _authorizationManager);
           // _services[entityType] = service;
            return Result<IService<T>>.Success((IService<T>)service);
        }

        #region Délégation Database Management

        public Task<Result> Create()
            => _databaseManager.CreateAsync();

        public Task<Result> DeleteCurrentDatabase()
            => _databaseManager.DeleteCurrentDatabaseAsync(_services);

        public void SetPaths(string folderPath, string fileName)
            => _databaseManager.SetPaths(folderPath, fileName);

        public void SetPaths()
        => _databaseManager.SetPaths();

        #endregion

        #region Délégation Connection Management

        /// <summary>
        /// Établit la connexion à la base de données
        /// </summary>
        public Task<Result> ConnectAsync()
            => _connectionManager.ConnectAsync();

        /// <summary>
        /// Ferme la connexion à la base de données
        /// </summary>
        public Task<Result> DisconnectAsync()
            => _connectionManager.DisconnectAsync();

        /// <summary>
        /// Vérifie si la connexion est valide
        /// </summary>
        public Task<bool> IsConnectionValidAsync()
            => _connectionManager.IsConnectionValidAsync();

        /// <summary>
        /// Effectue un health check de la base de données
        /// </summary>
        public Task<HealthCheckResult> CheckHealthAsync()
            => _healthChecker.CheckHealthAsync(_databaseManager.FullPath, _databaseManager.IsInMemory);

        /// <summary>
        /// Flush les données en attente
        /// </summary>
        public Task<Result> FlushAsync()
            => _connectionManager.FlushAsync();

        /// <summary>
        /// Exécute des opérations dans une transaction
        /// </summary>
        public Task<Result> ExecuteInTransactionAsync(Func<Task> operations)
            => _connectionManager.ExecuteInTransactionAsync(operations);

        /// <summary>
        /// Exécute des opérations dans une transaction avec résultat
        /// </summary>
        public Task<Result<T>> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
            => _connectionManager.ExecuteInTransactionAsync(operation);

        /// <summary>
        /// Ferme les connexions sans disposer le manager
        /// </summary>
        public async Task<Result> CloseConnectionsAsync()
        {
            var result = await _connectionManager.DisconnectAsync();
            _services.Clear();
            SqliteConnection.ClearAllPools();
            return result;
        }
        #endregion


        // 2. Added : New methods for queries
        #region Query Operations (éviter de charger tout en mémoire)

        /// <summary>
        /// Get a Queryable for custom request
        /// </summary>
        public Result<IQueryable<T>> Query<T>() where T : class, IBaseEntity
        {
            try
            {
                if (_dbContext == null)
                    return Result<IQueryable<T>>.Failure("DbContext is null");

                if (!IsConnected)
                    return Result<IQueryable<T>>.Failure("Not connected to database");

                var query = _dbContext.Set<T>().AsQueryable();
                return Result<IQueryable<T>>.Success(query);
            }
            catch (Exception ex)
            {
                return Result<IQueryable<T>>.Failure($"Failed to create query: {ex.Message}");
            }
        }

        /// <summary>
        /// GEt a IQueryable as not tracking for read only (more performance)
        /// </summary>
        public Result<IQueryable<T>> QueryNoTracking<T>() where T : class, IBaseEntity
        {
            try
            {
                if (_dbContext == null)
                    return Result<IQueryable<T>>.Failure("DbContext is null");

                if (!IsConnected)
                    return Result<IQueryable<T>>.Failure("Not connected to database");

                var query = _dbContext.Set<T>().AsNoTracking().AsQueryable();
                return Result<IQueryable<T>>.Success(query);
            }
            catch (Exception ex)
            {
                return Result<IQueryable<T>>.Failure($"Failed to create query: {ex.Message}");
            }
        }

        

        /// <summary>
        /// paged query
        /// </summary>
        public async Task<Result<List<T>>> GetPagedAsync<T>(int pageNumber, int pageSize, Expression<Func<T, bool>> predicate = null) where T : class, IBaseEntity
        {
            try
            {
                if (_dbContext == null)
                    return Result<List<T>>.Failure("DbContext is null");

                if (!IsConnected)
                    return Result<List<T>>.Failure("Not connected to database");

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 10;

                var query = _dbContext.Set<T>().AsQueryable();

                if (predicate != null)
                    query = query.Where(predicate);

                var results = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _connectionManager.UpdateActivity();
                return Result<List<T>>.Success(results);
            }
            catch (Exception ex)
            {
                return Result<List<T>>.Failure($"Paged query failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get numbers of element with a filter
        /// </summary>
        public async Task<Result<int>> CountAsync<T>(Expression<Func<T, bool>> predicate = null) where T : class, IBaseEntity
        {
            try
            {
                if (_dbContext == null)
                    return Result<int>.Failure("DbContext is null");

                if (!IsConnected)
                    return Result<int>.Failure("Not connected to database");

                var query = _dbContext.Set<T>().AsQueryable();

                if (predicate != null)
                    query = query.Where(predicate);

                var count = await query.CountAsync();

                _connectionManager.UpdateActivity();
                return Result<int>.Success(count);
            }
            catch (Exception ex)
            {
                return Result<int>.Failure($"Count query failed: {ex.Message}");
            }
        }

        /// <summary>
        /// get the first element of a filter
        /// </summary>
        public async Task<Result<T>> FirstOrDefaultAsync<T>(Expression<Func<T, bool>> predicate) where T : class, IBaseEntity
        {
            try
            {
                if (_dbContext == null)
                    return Result<T>.Failure("DbContext is null");

                if (!IsConnected)
                    return Result<T>.Failure("Not connected to database");

                var result = await _dbContext.Set<T>()
                    .FirstOrDefaultAsync(predicate);

                _connectionManager.UpdateActivity();

                if (result == null)
                    return Result<T>.Failure("No entity found matching the criteria");

                return Result<T>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<T>.Failure($"Query failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Verify existance with a filter
        /// </summary>
        public async Task<Result<bool>> AnyAsync<T>(Expression<Func<T, bool>> predicate) where T : class, IBaseEntity
        {
            try
            {
                if (_dbContext == null)
                    return Result<bool>.Failure("DbContext is null");

                if (!IsConnected)
                    return Result<bool>.Failure("Not connected to database");

                var exists = await _dbContext.Set<T>()
                    .AnyAsync(predicate);

                _connectionManager.UpdateActivity();
                return Result<bool>.Success(exists);
            }
            catch (Exception ex)
            {
                return Result<bool>.Failure($"Query failed: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// REfresh database context
        /// </summary>
        public async Task<Result> RefreshContextAsync()
        {
            try
            {
                // Fermer l'ancienne connexion proprement
                if (_dbContext != null)
                {
                    await _connectionManager.DisconnectAsync();
                    await _dbContext.DisposeAsync();
                }

                // Créer un nouveau contexte
                _dbContext = _dbContextbuilder.Invoke();

                // Mettre à jour dans tous les gestionnaires
                await _connectionManager.UpdateContext(_dbContext);
                _databaseManager.UpdateContext(_dbContext);

                // Réinitialiser les services
                _services.Clear();

                // Mettre à jour le path si nécessaire
                if (!_databaseManager.IsInMemory)
                {
                    _databaseManager.SetPaths();
                }

                return Result.Success("Context refreshed successfully");
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to refresh context: {ex.Message}");
            }
        }
    }
}
