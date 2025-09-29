using Microsoft.Data.Sqlite;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Core.Models;
using MatthL.SqliteEF.Core.Services;
using System.Collections.Concurrent;
using MatthL.ResultLogger.Core.Models;

namespace MatthL.SqliteEF.Core.Managers
{
    public class SQLManager
    {
        private readonly ConcurrentDictionary<Type, object> _services;
        private RootDbContext _dbContext;
        private readonly Func<RootDbContext> _dbContextbuilder;
        private readonly IAuthorizationManager _authorizationManager;

        // Gestionnaires délégués
        private readonly SQLCrudOperations _crudOperations;
        private readonly SQLConnectionManager _connectionManager;
        private readonly SQLDatabaseManager _databaseManager;
        private readonly SQLHealthChecker _healthChecker;

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
            _crudOperations = new SQLCrudOperations(this);
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
            _crudOperations = new SQLCrudOperations(this);
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

            if (!_services.TryGetValue(entityType, out var service))
            {
                service = new BaseService<T>(_dbContext, _authorizationManager);
                _services[entityType] = service;
            }

            return Result<IService<T>>.Success((IService<T>)service);
        }

        #region Délégation des opérations CRUD

        public Task<Result> AddAsync<T>(T entity) where T : class, IBaseEntity
            => _crudOperations.AddAsync(entity);

        public Task<Result> UpdateAsync<T>(T entity) where T : class, IBaseEntity
            => _crudOperations.UpdateAsync(entity);

        public Task<Result> DeleteAsync<T>(T entity) where T : class, IBaseEntity
            => _crudOperations.DeleteAsync(entity);

        public Task<Result<List<T>>> GetAllAsync<T>() where T : class, IBaseEntity
            => _crudOperations.GetAllAsync<T>();

        public Task<Result<T>> GetByIdAsync<T>(int id) where T : class, IBaseEntity
            => _crudOperations.GetByIdAsync<T>(id);

        public Task<Result> AddRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
            => _crudOperations.AddRangeAsync(entities);

        public Task<Result> UpdateRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
            => _crudOperations.UpdateRangeAsync(entities);

        public Task<Result> DeleteRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
            => _crudOperations.DeleteRangeAsync(entities);

        public Task<Result> DeleteAllAsync<T>() where T : class, IBaseEntity
            => _crudOperations.DeleteAllAsync<T>();

        public Task<Result> AddOrUpdateAsync<T>(T entity) where T : class, IBaseEntity
            => _crudOperations.AddOrUpdateAsync(entity);

        public Task<Result<bool>> AnyExistAsync<T>() where T : class, IBaseEntity
            => _crudOperations.AnyExistAsync<T>();

        public Result<bool> IsSavable<T>(T entity) where T : class, IBaseEntity
            => _crudOperations.IsSavable(entity);

        #endregion

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


        /// <summary>
        /// Rafraîchit le contexte de base de données
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
                _connectionManager.UpdateContext(_dbContext);
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
