using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Models;
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
        private SQLConnectionManager SQLConnectionManager { get; set; }

        public SQLHealthChecker(SQLConnectionManager _SQLConnectionManager)
        {
            SQLConnectionManager = _SQLConnectionManager;
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
                details["state"] = SQLConnectionManager.CurrentState.ToString();
                details["isConnected"] = SQLConnectionManager.IsConnected;
                details["lastActivity"] = SQLConnectionManager.LastActivityTime?.ToString("O") ?? "Never";
                details["lastConnection"] = SQLConnectionManager.LastConnectionTime?.ToString("O") ?? "Never";

                if (!SQLConnectionManager.IsConnected)
                {
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Unhealthy,
                        Description = "Database disconnected",
                        Details = details
                    };
                }

                if (SQLConnectionManager.DbContext == null)
                {
                    details["error"] = "DbContext is null";
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Unhealthy,
                        Description = "No database context",
                        Details = details
                    };
                }

                // Test de performance
                var sw = Stopwatch.StartNew();
                await SQLConnectionManager.DbContext.Database.ExecuteSqlRawAsync("SELECT 1");
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

                SQLConnectionManager.UpdateActivity();

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
                    await SQLConnectionManager.ChangeStateAsync(ConnectionState.Corrupted);
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
    }
}
