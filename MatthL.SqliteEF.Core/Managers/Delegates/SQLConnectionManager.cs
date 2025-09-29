using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MatthL.SqliteEF.Core.Enums;
using System.Runtime.CompilerServices;
using MatthL.SqliteEF.Core.Models;
using MatthL.ResultLogger.Core.Models;

[assembly: InternalsVisibleTo("SQLiteManager.Tests")]
namespace MatthL.SqliteEF.Core.Managers.Delegates
{
    internal class SQLConnectionManager
    {
        private RootDbContext _dbContext;
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        public DateTime? LastConnectionTime { get;set; }
        public DateTime? LastActivityTime { get;set; }

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
        public RootDbContext DbContext => _dbContext;
        public SQLConnectionManager(RootDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Met à jour le contexte de base de données (appelé par SQLManager lors d'un refresh)
        /// </summary>
        public void UpdateContext(RootDbContext newContext)
        {
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

                await ChangeStateAsync(ConnectionState.Connected);
                return Result.Success("Connected successfully");
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
    }

    
}
