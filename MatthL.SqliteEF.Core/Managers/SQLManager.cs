using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Core.Models;
using MatthL.SqliteEF.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace MatthL.SqliteEF.Core.Managers
{
    public partial class SQLManager
    {
        private readonly IDbContextFactory<RootDbContext> _contextFactory;
        private readonly SemaphoreSlim _readLock = new SemaphoreSlim(5, 5);
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private readonly IAuthorizationManager _authorizationManager;

        // Gestionnaires délégués
        private readonly SQLConnectionManager _connectionManager;
        private readonly SQLDatabaseManager _databaseManager;
        private readonly SQLHealthChecker _healthChecker;

        public SQLConnectionManager ConnectionManager => _connectionManager;

        /// <summary>
        /// Access to the db context to bypass the functions, use it carefully
        /// Note: Creates a new disposable context each time
        /// </summary>
        public RootDbContext DbContext => _contextFactory.CreateDbContext();

        /// <summary>
        /// Main Builder
        /// </summary>
        /// <param name="contextFactory">the db context factory, must have been set inside the IOC</param>
        /// <param name="folderPath">Path to the folder containing the database file</param>
        /// <param name="fileName">Name of the database file (without extension)</param>
        /// <param name="extension">File extension (e.g., ".db", ".sqlite")</param>
        /// <param name="authorizationManager">Authorization manager for controlling access</param>
        public SQLManager(
            IDbContextFactory<RootDbContext> contextFactory,
            string folderPath = "",
            string fileName = "database",
            string extension = ".db",
            IAuthorizationManager authorizationManager = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _authorizationManager = authorizationManager ?? new DefaultAuthorizationManager();

            // Initialiser les managers délégués avec la factory
            _connectionManager = new SQLConnectionManager(_contextFactory);
            _databaseManager = new SQLDatabaseManager(
                _contextFactory,
                _connectionManager,
                folderPath,
                fileName,
                extension);
            _healthChecker = new SQLHealthChecker(_contextFactory, _connectionManager);

            // Configurer les chemins si fournis
            if (!string.IsNullOrEmpty(folderPath))
            {
                _databaseManager.SetPaths(folderPath, fileName, extension);
                _databaseManager.SetPaths(); // Valider les chemins
            }
        }

        /// <summary>
        /// Alternative constructor with full file path
        /// </summary>
        /// <param name="contextFactory">the db context factory</param>
        /// <param name="fileFullPath">Full path to the database file (including extension)</param>
        /// <param name="authorizationManager">Authorization manager</param>
        public SQLManager(
            IDbContextFactory<RootDbContext> contextFactory,
            string fileFullPath,
            IAuthorizationManager authorizationManager = null)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _authorizationManager = authorizationManager ?? new DefaultAuthorizationManager();

            // Extraire le dossier, le nom et l'extension du chemin complet
            var folderPath = Path.GetDirectoryName(fileFullPath);
            var fileName = Path.GetFileNameWithoutExtension(fileFullPath);
            var extension = Path.GetExtension(fileFullPath);

            // Initialiser les managers délégués
            _connectionManager = new SQLConnectionManager(_contextFactory);
            _databaseManager = new SQLDatabaseManager(
                _contextFactory,
                _connectionManager,
                folderPath,
                fileName,
                extension);
            _healthChecker = new SQLHealthChecker(_contextFactory, _connectionManager);

            // Configurer les chemins
            if (!string.IsNullOrEmpty(folderPath))
            {
                _databaseManager.SetPaths(folderPath, fileName, extension);
                _databaseManager.SetPaths(); // Valider les chemins
            }
        }

        // Propriétés publiques
        public string GetFullPath => _databaseManager.FullPath;
        public string GetFolderPath => _databaseManager.FolderPath;
        public string GetFileName => _databaseManager.FileName;
        public string GetFileExtension => _databaseManager.FileExtension;
        public long GetFileSize => _databaseManager.FileSize;
        public bool IsInMemory => _databaseManager.IsInMemory;

        #region Délégation Database Management

        /// <summary>
        /// Create the database and apply migrations
        /// </summary>
        public Task<Result> Create()
            => _databaseManager.CreateAsync();

        /// <summary>
        /// Delete the current database file
        /// </summary>
        public Task<Result> DeleteCurrentDatabase()
            => _databaseManager.DeleteCurrentDatabaseAsync(new ConcurrentDictionary<Type, object>());

        /// <summary>
        /// Set database paths with extension
        /// </summary>
        public void SetPaths(string folderPath, string fileName, string extension = null)
            => _databaseManager.SetPaths(folderPath, fileName, extension);

        /// <summary>
        /// Validate that paths are correctly set
        /// </summary>
        public void SetPaths()
            => _databaseManager.SetPaths();

        /// <summary>
        /// Change the file extension
        /// </summary>
        public void SetExtension(string extension)
            => _databaseManager.SetExtension(extension);

        /// <summary>
        /// Get detailed database file information
        /// </summary>
        public Result<DatabaseFileInfo> GetDatabaseFileInfo()
            => _databaseManager.GetDatabaseFileInfo();

        #endregion

        #region CONNECTION REGION 

        // Propriétés déléguées pour l'état de connexion
        public ConnectionState CurrentState => _connectionManager.CurrentState;
        public bool IsConnected => _connectionManager.IsConnected;
        public DateTime? LastActivity => _connectionManager.LastActivityTime;
        public DateTime? LastConnection => _connectionManager.LastConnectionTime;

        // Événement relayé
        public event EventHandler<ConnectionState> ConnectionStateChanged
        {
            add => _connectionManager.ConnectionStateChanged += value;
            remove => _connectionManager.ConnectionStateChanged -= value;
        }

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
        /// Effectue un health check rapide (ping uniquement)
        /// </summary>
        public Task<HealthCheckResult> QuickHealthCheckAsync()
            => _healthChecker.QuickHealthCheckAsync();

        /// <summary>
        /// Obtient des statistiques sur la base de données
        /// </summary>
        public Task<Result<DatabaseStatistics>> GetDatabaseStatisticsAsync()
            => _healthChecker.GetDatabaseStatisticsAsync(_databaseManager.FullPath);

        /// <summary>
        /// Flush les données en attente
        /// </summary>
        public Task<Result> FlushAsync()
            => _connectionManager.FlushAsync();

        /// <summary>
        /// Ferme les connexions sans disposer le manager
        /// </summary>
        public async Task<Result> CloseConnectionsAsync()
        {
            var result = await _connectionManager.DisconnectAsync();
            SqliteConnection.ClearAllPools();
            return result;
        }

        /// <summary>
        /// Get current concurrency configuration
        /// </summary>
        public Task<Result<Dictionary<string, string>>> GetConcurrencyConfigAsync()
            => _connectionManager.GetConcurrencyConfigAsync();

        /// <summary>
        /// Execute raw SQL command (use with caution)
        /// </summary>
        public Task<Result> ExecuteRawSqlAsync(string sql)
            => _connectionManager.ExecuteRawSqlAsync(sql);

        #endregion
    
    }
}
