using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MatthL.SqliteEF.Core.Enums;
using System.Runtime.CompilerServices;
using MatthL.SqliteEF.Core.Models;
using MatthL.ResultLogger.Core.Models;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("SQLiteManager.Tests")]
namespace MatthL.SqliteEF.Core.Managers.Delegates
{
    public class SQLConnectionManager
    {
        private RootDbContext _dbContext;
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        public DateTime? LastConnectionTime { get;set; }
        public DateTime? LastActivityTime { get;set; }

        // Événement pour notifier les changements d'état
        public event EventHandler<ConnectionState> ConnectionStateChanged;
        public RootDbContext DbContext => _dbContext;

        // Propriétés publiques thread-safe
        public ConnectionState CurrentState
        {
            get
            {
                _stateLock.Wait();
                try
                {
                    return _connectionState;
                }
                finally
                {
                    _stateLock.Release();
                }
            }
        }

        public bool IsConnected => CurrentState == ConnectionState.Connected;
        
        public SQLConnectionManager(RootDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Met à jour le contexte de base de données (appelé par SQLManager lors d'un refresh)
        /// </summary>
        public async Task UpdateContext(RootDbContext newContext)
        {
            if(_connectionState == ConnectionState.Connected)
            {
                await DisconnectAsync();
            }

            _dbContext = newContext;
            // Reset de l'état car on a un nouveau contexte
            _ = ChangeStateAsync(ConnectionState.Disconnected);
            LastActivityTime = null;
            LastConnectionTime = null;
        }

        /// <summary>
        /// Change l'état de connexion de manière thread-safe
        /// </summary>
        public async Task<Result> ChangeStateAsync(ConnectionState newState)
        {
            await _stateLock.WaitAsync();
            try
            {
                var oldState = _connectionState;
                _connectionState = newState;

                if (newState == ConnectionState.Connected)
                {
                    LastActivityTime = DateTime.UtcNow;
                    LastConnectionTime = DateTime.UtcNow;
                }

                // Déclencher l'événement si l'état a changé
                if (oldState != newState)
                {
                    ConnectionStateChanged?.Invoke(this, newState);
                    return Result.Success($"State changed from {oldState} to {newState}");
                }

                return Result.Success("State unchanged");
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// Établit la connexion à la base de données
        /// </summary>
        public async Task<Result> ConnectAsync()
        {
            try
            {
                if (IsConnected)
                {
                    return Result.Success("Already connected");
                }

                await ChangeStateAsync(ConnectionState.Connecting);

                if (_dbContext == null)
                {
                    await ChangeStateAsync(ConnectionState.Disconnected);
                    return Result.Failure("DbContext is null, need to refresh context");
                }

                await _dbContext.Database.OpenConnectionAsync();

                // Test de connexion
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");

                await ConfigureConcurrentAccessAsync();

                await ChangeStateAsync(ConnectionState.Connected);
                return Result.Success("Connected successfully with concurrent access enabled");
            }
            catch (SqliteException sqlEx)
            {
                await ChangeStateAsync(ConnectionState.Disconnected);
                return Result.Failure($"SQLite connection failed: {sqlEx.Message}");
            }
            catch (Exception ex)
            {
                await ChangeStateAsync(ConnectionState.Disconnected);
                return Result.Failure($"Connection failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Ferme la connexion à la base de données
        /// </summary>
        public async Task<Result> DisconnectAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    return Result.Success("Already disconnected");
                }

                if (_dbContext != null)
                {
                    // Optimisations avant fermeture
                    try
                    {
                        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)");
                        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA optimize");
                    }
                    catch (Exception ex)
                    {
                        // Log mais ne pas faire échouer la déconnexion
                        Result.Failure($"Warning during optimization: {ex.Message}");
                    }

                    await _dbContext.Database.CloseConnectionAsync();
                }

                await ChangeStateAsync(ConnectionState.Disconnected);
                return Result.Success("Disconnected successfully");
            }
            catch (Exception ex)
            {
                await ChangeStateAsync(ConnectionState.Disconnected);
                return Result.Failure($"Disconnection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Vérifie si la connexion est valide
        /// </summary>
        public async Task<bool> IsConnectionValidAsync()
        {
            try
            {
                if (_dbContext == null || !IsConnected)
                    return false;

                // Test simple de connexion
                var canConnect = await _dbContext.Database.CanConnectAsync();

                if (!canConnect)
                {
                    await ChangeStateAsync(ConnectionState.Disconnected);
                    return false;
                }

                // Test plus approfondi avec une requête
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");

                UpdateActivity();
                return true;
            }
            catch (SqliteException sqlEx) when (sqlEx.SqliteErrorCode == 14) // SQLITE_CANTOPEN
            {
                await ChangeStateAsync(ConnectionState.Corrupted);
                return false;
            }
            catch (Exception)
            {
                await ChangeStateAsync(ConnectionState.Disconnected);
                return false;
            }
        }

        /// <summary>
        /// Met à jour l'heure de dernière activité
        /// </summary>
        public void UpdateActivity()
        {
            LastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Exécute une transaction
        /// </summary>
        public async Task<Result> ExecuteInTransactionAsync(Func<Task> operations)
        {
            if (!IsConnected)
            {
                return Result.Failure("Not connected to database");
            }

            if (_dbContext == null)
            {
                return Result.Failure("DbContext is null");
            }

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                await operations();
                await transaction.CommitAsync();
                UpdateActivity();
                return Result.Success("Transaction completed");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Result.Failure($"Transaction rolled back: {ex.Message}");
            }
        }

        /// <summary>
        /// Exécute une transaction avec résultat
        /// </summary>
        public async Task<Result<T>> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            if (!IsConnected)
            {
                return Result<T>.Failure("Not connected to database");
            }

            if (_dbContext == null)
            {
                return Result<T>.Failure("DbContext is null");
            }

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var result = await operation();
                await transaction.CommitAsync();
                UpdateActivity();
                return Result<T>.Success(result);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Result<T>.Failure($"Transaction rolled back: {ex.Message}");
            }
        }

        /// <summary>
        /// Flush les données en attente
        /// </summary>
        public async Task<Result> FlushAsync()
        {
            try
            {
                if (_dbContext != null && IsConnected)
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL)");
                    UpdateActivity();
                    return Result.Success("Flush completed");
                }
                return Result.Failure("Not connected or context is null");
            }
            catch (Exception ex)
            {
                return Result.Failure($"Flush failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Configure SQLite for concurrent access (WAL mode + optimisations)
        /// </summary>
        private async Task ConfigureConcurrentAccessAsync()
        {
            try
            {
                // Activer le mode WAL (Write-Ahead Logging) pour permettre les lectures concurrentes
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

                // Configurer le busy timeout (attendre jusqu'à 5 secondes si la DB est verrouillée)
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");

                // Activer le cache partagé pour améliorer les performances avec plusieurs connexions
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA cache_size=-20000;"); // 20MB de cache

                // Optimisation de la synchronisation (NORMAL = bon compromis perf/sécurité)
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");

                // Taille de page optimale pour les performances
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA page_size=4096;");

                // Activer les foreign keys
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");

                // Mode de verrouillage pour permettre plus de concurrence
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA locking_mode=NORMAL;");
            }
            catch (Exception ex)
            {
                // Log l'erreur mais ne pas faire échouer la connexion
                // La plupart de ces PRAGMAs sont des optimisations
                Result.Failure($"Warning: Some concurrent access configurations failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Verify Concurrent access configuration
        /// </summary>
        public async Task<Result<Dictionary<string, string>>> GetConcurrencyConfigAsync()
        {
            try
            {
                if (_dbContext == null || !IsConnected)
                    return Result<Dictionary<string, string>>.Failure("Not connected to database");

                var config = new Dictionary<string, string>();

                // Vérifier le mode journal
                var journalMode = await ExecutePragmaQueryAsync("journal_mode");
                config["journal_mode"] = journalMode;

                // Vérifier le busy timeout
                var busyTimeout = await ExecutePragmaQueryAsync("busy_timeout");
                config["busy_timeout"] = busyTimeout;

                // Vérifier le cache size
                var cacheSize = await ExecutePragmaQueryAsync("cache_size");
                config["cache_size"] = cacheSize;

                // Vérifier synchronous
                var synchronous = await ExecutePragmaQueryAsync("synchronous");
                config["synchronous"] = synchronous;

                // Vérifier locking mode
                var lockingMode = await ExecutePragmaQueryAsync("locking_mode");
                config["locking_mode"] = lockingMode;

                return Result<Dictionary<string, string>>.Success(config);
            }
            catch (Exception ex)
            {
                return Result<Dictionary<string, string>>.Failure($"Failed to get concurrency config: {ex.Message}");
            }
        }

        // MÉTHODE HELPER pour exécuter les requêtes PRAGMA
        private async Task<string> ExecutePragmaQueryAsync(string pragmaName)
        {
            try
            {
                var connection = _dbContext.Database.GetDbConnection();
                using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA {pragmaName};";

                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "unknown";
            }
            catch
            {
                return "error";
            }
        }
    }

    
}
