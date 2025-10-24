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
        private readonly Func<string, RootDbContext> _contextFactory;
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private SqliteConnection _persistentConnection;
        public string DatabasePath { get; set; } = ":memory:";

        public DateTime? LastConnectionTime { get; set; }
        public DateTime? LastActivityTime { get; set; }

        // Événement pour notifier les changements d'état
        public event EventHandler<ConnectionState> ConnectionStateChanged;

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

        public SQLConnectionManager(Func<string, RootDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
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

                // Créer un context pour tester et configurer la connexion
                using var context = _contextFactory(DatabasePath);

                // Test de connexion
                var canConnect = await context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    await ChangeStateAsync(ConnectionState.Disconnected);
                    return Result.Failure("Cannot connect to database");
                }

                await context.Database.OpenConnectionAsync();

                // Test avec une requête simple
                await context.Database.ExecuteSqlRawAsync("SELECT 1");

                // Configurer l'accès concurrent
                await ConfigureConcurrentAccessAsync(context);

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

                using var context = _contextFactory(DatabasePath);

                // Optimisations avant fermeture
                try
                {
                    await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)");
                    await context.Database.ExecuteSqlRawAsync("PRAGMA optimize");
                }
                catch (Exception ex)
                {
                    // Log mais ne pas faire échouer la déconnexion
                    Console.WriteLine($"Warning during optimization: {ex.Message}");
                }

                await context.Database.CloseConnectionAsync();

                // Fermer la connexion persistante si elle existe
                if (_persistentConnection != null)
                {
                    await _persistentConnection.CloseAsync();
                    await _persistentConnection.DisposeAsync();
                    _persistentConnection = null;
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
                if (!IsConnected)
                    return false;

                using var context = _contextFactory(DatabasePath);

                // Test simple de connexion
                var canConnect = await context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    await ChangeStateAsync(ConnectionState.Disconnected);
                    return false;
                }

                // Test plus approfondi avec une requête
                await context.Database.ExecuteSqlRawAsync("SELECT 1");

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
        /// Flush les données en attente
        /// </summary>
        public async Task<Result> FlushAsync()
        {
            try
            {
                if (!IsConnected)
                    return Result.Failure("Not connected to database");

                using var context = _contextFactory(DatabasePath);

                await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL)");
                UpdateActivity();
                return Result.Success("Flush completed");
            }
            catch (Exception ex)
            {
                return Result.Failure($"Flush failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Configure SQLite for concurrent access (WAL mode + optimisations)
        /// </summary>
        private async Task ConfigureConcurrentAccessAsync(RootDbContext context)
        {
            try
            {
                // Activer le mode WAL (Write-Ahead Logging) pour permettre les lectures concurrentes
                await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

                // Configurer le busy timeout (attendre jusqu'à 5 secondes si la DB est verrouillée)
                await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");

                // Activer le cache partagé pour améliorer les performances avec plusieurs connexions
                await context.Database.ExecuteSqlRawAsync("PRAGMA cache_size=-20000;"); // 20MB de cache

                // Optimisation de la synchronisation (NORMAL = bon compromis perf/sécurité)
                await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");

                // Taille de page optimale pour les performances
                await context.Database.ExecuteSqlRawAsync("PRAGMA page_size=4096;");

                // Activer les foreign keys
                await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");

                // Mode de verrouillage pour permettre plus de concurrence
                await context.Database.ExecuteSqlRawAsync("PRAGMA locking_mode=NORMAL;");
            }
            catch (Exception ex)
            {
                // Log l'erreur mais ne pas faire échouer la connexion
                // La plupart de ces PRAGMAs sont des optimisations
                Console.WriteLine($"Warning: Some concurrent access configurations failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Verify Concurrent access configuration
        /// </summary>
        public async Task<Result<Dictionary<string, string>>> GetConcurrencyConfigAsync()
        {
            try
            {
                if (!IsConnected)
                    return Result<Dictionary<string, string>>.Failure("Not connected to database");

                using var context = _contextFactory(DatabasePath);

                var config = new Dictionary<string, string>();

                // Vérifier le mode journal
                config["journal_mode"] = await ExecutePragmaQueryAsync(context, "journal_mode");

                // Vérifier le busy timeout
                config["busy_timeout"] = await ExecutePragmaQueryAsync(context, "busy_timeout");

                // Vérifier le cache size
                config["cache_size"] = await ExecutePragmaQueryAsync(context, "cache_size");

                // Vérifier synchronous
                config["synchronous"] = await ExecutePragmaQueryAsync(context, "synchronous");

                // Vérifier locking mode
                config["locking_mode"] = await ExecutePragmaQueryAsync(context, "locking_mode");

                // Vérifier foreign keys
                config["foreign_keys"] = await ExecutePragmaQueryAsync(context, "foreign_keys");

                return Result<Dictionary<string, string>>.Success(config);
            }
            catch (Exception ex)
            {
                return Result<Dictionary<string, string>>.Failure($"Failed to get concurrency config: {ex.Message}");
            }
        }

        /// <summary>
        /// MÉTHODE HELPER pour exécuter les requêtes PRAGMA
        /// </summary>
        private async Task<string> ExecutePragmaQueryAsync(RootDbContext context, string pragmaName)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA {pragmaName};";

                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? "unknown";
            }
            catch
            {
                return "error";
            }
        }

        /// <summary>
        /// Execute a raw SQL command
        /// </summary>
        public async Task<Result> ExecuteRawSqlAsync(string sql)
        {
            try
            {
                if (!IsConnected)
                    return Result.Failure("Not connected to database");

                using var context = _contextFactory(DatabasePath);

                await context.Database.ExecuteSqlRawAsync(sql);
                UpdateActivity();
                return Result.Success("SQL executed successfully");
            }
            catch (Exception ex)
            {
                return Result.Failure($"SQL execution failed: {ex.Message}");
            }
        }
    }


}
