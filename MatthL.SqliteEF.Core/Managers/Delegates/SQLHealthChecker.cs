using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Models;
using MatthL.SqliteEF.Core.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
[assembly: InternalsVisibleTo("SQLiteManager.Tests")]
namespace MatthL.SqliteEF.Core.Managers.Delegates
{
    public class HealthCheckResult
    {
        public HealthStatus Status { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    internal class SQLHealthChecker
    {
        private readonly Func<string, RootDbContext> _contextFactory;
        private readonly SQLConnectionManager _connectionManager;

        public SQLHealthChecker(
            Func<string, RootDbContext> contextFactory,
            SQLConnectionManager connectionManager)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        }

        /// <summary>
        /// Effectue un health check complet de la connexion
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(string databasePath = null, bool isInMemory = false)
        {
            var details = new Dictionary<string, object>();

            try
            {
                // État de base
                details["state"] = _connectionManager.CurrentState.ToString();
                details["isConnected"] = _connectionManager.IsConnected;
                details["lastActivity"] = _connectionManager.LastActivityTime?.ToString("O") ?? "Never";
                details["lastConnection"] = _connectionManager.LastConnectionTime?.ToString("O") ?? "Never";

                if (!_connectionManager.IsConnected)
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Unhealthy,
                        Description = "Database disconnected",
                        Details = details
                    };
                }

                // Test de performance avec un context temporaire
                using var context = _contextFactory(databasePath);

                var sw = Stopwatch.StartNew();
                await context.Database.ExecuteSqlRawAsync("SELECT 1");
                sw.Stop();

                details["pingMs"] = sw.ElapsedMilliseconds;

                // Vérifier l'espace disque pour SQLite (si pas in-memory)
                if (!isInMemory && !string.IsNullOrEmpty(databasePath) && File.Exists(databasePath))
                {
                    var fileInfo = new FileInfo(databasePath);
                    details["dbSizeMB"] = Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2);

                    // Vérifier les fichiers associés
                    if (File.Exists($"{databasePath}-wal"))
                    {
                        var walInfo = new FileInfo($"{databasePath}-wal");
                        details["walSizeMB"] = Math.Round(walInfo.Length / (1024.0 * 1024.0), 2);
                    }

                    if (File.Exists($"{databasePath}-shm"))
                    {
                        var shmInfo = new FileInfo($"{databasePath}-shm");
                        details["shmSizeMB"] = Math.Round(shmInfo.Length / (1024.0 * 1024.0), 2);
                    }

                    // Vérifier l'espace disque disponible
                    try
                    {
                        var driveInfo = new DriveInfo(Path.GetPathRoot(databasePath));
                        details["freeSpaceGB"] = Math.Round(driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 2);
                        details["totalSpaceGB"] = Math.Round(driveInfo.TotalSize / (1024.0 * 1024.0 * 1024.0), 2);
                    }
                    catch
                    {
                        // Ignorer les erreurs de lecture du disque
                    }
                }

                // Vérifier l'intégrité de la base de données
                try
                {
                    var integrityCheck = await CheckDatabaseIntegrityAsync(context);
                    details["integrityCheck"] = integrityCheck ? "OK" : "FAILED";
                }
                catch (Exception ex)
                {
                    details["integrityCheck"] = $"ERROR: {ex.Message}";
                }

                // Déterminer le statut
                HealthStatus status;
                string description;

                if (sw.ElapsedMilliseconds > 1000)
                {
                    status = HealthStatus.Degraded;
                    description = "Database operational but slow";
                }
                else
                {
                    status = HealthStatus.Healthy;
                    description = "Database operational";
                }

                _connectionManager.UpdateActivity();

                return new HealthCheckResult
                {
                    Status = status,
                    Description = description,
                    Details = details
                };
            }
            catch (SqliteException sqlEx)
            {
                details["error"] = sqlEx.Message;
                details["errorCode"] = sqlEx.SqliteErrorCode;

                // Marquer comme corrompu si c'est une erreur de corruption
                if (sqlEx.SqliteErrorCode == 11) // SQLITE_CORRUPT
                {
                    await _connectionManager.ChangeStateAsync(ConnectionState.Corrupted);
                }

                return new HealthCheckResult
                {
                    Status = HealthStatus.Unhealthy,
                    Description = "Database check failed",
                    Details = details
                };
            }
            catch (Exception ex)
            {
                details["error"] = ex.Message;

                return new HealthCheckResult
                {
                    Status = HealthStatus.Unhealthy,
                    Description = "Database check failed",
                    Details = details
                };
            }
        }

        /// <summary>
        /// Vérifie l'intégrité de la base de données
        /// </summary>
        private async Task<bool> CheckDatabaseIntegrityAsync(RootDbContext context)
        {
            try
            {
                var connection = context.Database.GetDbConnection();

                // S'assurer que la connexion est ouverte
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                await using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA integrity_check;";

                var result = await command.ExecuteScalarAsync();

                // SQLite retourne "ok" si tout va bien (insensible à la casse)
                // Peut aussi retourner "ok" avec des espaces, donc on trim
                var resultString = result?.ToString()?.Trim();

                return !string.IsNullOrEmpty(resultString) &&
                       resultString.Equals("ok", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                // Log l'erreur pour debug
                Console.WriteLine($"Integrity check error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Effectue un health check rapide (ping uniquement)
        /// </summary>
        public async Task<HealthCheckResult> QuickHealthCheckAsync()
        {
            var details = new Dictionary<string, object>
            {
                ["state"] = _connectionManager.CurrentState.ToString(),
                ["isConnected"] = _connectionManager.IsConnected
            };

            try
            {
                if (!_connectionManager.IsConnected)
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Unhealthy,
                        Description = "Database disconnected",
                        Details = details
                    };
                }

                using var context = _contextFactory(_connectionManager.DatabasePath);

                var sw = Stopwatch.StartNew();
                await context.Database.ExecuteSqlRawAsync("SELECT 1");
                sw.Stop();

                details["pingMs"] = sw.ElapsedMilliseconds;

                var status = sw.ElapsedMilliseconds > 1000 ? HealthStatus.Degraded : HealthStatus.Healthy;
                var description = sw.ElapsedMilliseconds > 1000 ? "Slow response" : "OK";

                return new HealthCheckResult
                {
                    Status = status,
                    Description = description,
                    Details = details
                };
            }
            catch (Exception ex)
            {
                details["error"] = ex.Message;

                return new HealthCheckResult
                {
                    Status = HealthStatus.Unhealthy,
                    Description = "Quick check failed",
                    Details = details
                };
            }
        }

        /// <summary>
        /// Obtient des statistiques sur la base de données
        /// </summary>
        public async Task<Result<DatabaseStatistics>> GetDatabaseStatisticsAsync(string databasePath)
        {
            try
            {
                if (!_connectionManager.IsConnected)
                    return Result<DatabaseStatistics>.Failure("Database not connected");

                using var context = _contextFactory(_connectionManager.DatabasePath);

                var stats = new DatabaseStatistics();

                // Page count
                stats.PageCount = await GetPragmaIntValueAsync(context, "page_count");

                // Page size
                stats.PageSize = await GetPragmaIntValueAsync(context, "page_size");

                // Free pages
                stats.FreePageCount = await GetPragmaIntValueAsync(context, "freelist_count");

                // Calculate sizes
                stats.TotalSizeBytes = stats.PageCount * stats.PageSize;
                stats.FreeSizeBytes = stats.FreePageCount * stats.PageSize;
                stats.UsedSizeBytes = stats.TotalSizeBytes - stats.FreeSizeBytes;

                // File size (if not in-memory)
                if (!string.IsNullOrEmpty(databasePath) && File.Exists(databasePath))
                {
                    var fileInfo = new FileInfo(databasePath);
                    stats.FileSizeBytes = fileInfo.Length;

                    // WAL file
                    if (File.Exists($"{databasePath}-wal"))
                    {
                        var walInfo = new FileInfo($"{databasePath}-wal");
                        stats.WalSizeBytes = walInfo.Length;
                    }
                }

                // Get table count
                var connection = context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                var tableCount = await command.ExecuteScalarAsync();
                stats.TableCount = Convert.ToInt32(tableCount);

                return Result<DatabaseStatistics>.Success(stats);
            }
            catch (Exception ex)
            {
                return Result<DatabaseStatistics>.Failure($"Failed to get database statistics: {ex.Message}");
            }
        }

        private async Task<long> GetPragmaIntValueAsync(RootDbContext context, string pragmaName)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA {pragmaName};";

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt64(result);
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Statistiques de la base de données
    /// </summary>
    public class DatabaseStatistics
    {
        public long PageCount { get; set; }
        public long PageSize { get; set; }
        public long FreePageCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public long UsedSizeBytes { get; set; }
        public long FreeSizeBytes { get; set; }
        public long FileSizeBytes { get; set; }
        public long WalSizeBytes { get; set; }
        public int TableCount { get; set; }

        public string TotalSizeString => TotalSizeBytes.ToFileSizeString();
        public string UsedSizeString => UsedSizeBytes.ToFileSizeString();
        public string FreeSizeString => FreeSizeBytes.ToFileSizeString();
        public string FileSizeString => FileSizeBytes.ToFileSizeString();
        public string WalSizeString => WalSizeBytes.ToFileSizeString();
        public double UsagePercentage => TotalSizeBytes > 0 ? Math.Round((UsedSizeBytes / (double)TotalSizeBytes) * 100, 2) : 0;
    }
}
