using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Demo2
{
    /// <summary>
    /// Mock du SQLManager pour la démo sans base de données réelle
    /// </summary>
    public class MockSQLManager : SQLManager
    {
        private ConnectionState _mockState;
        private bool _isReading;
        private bool _isWriting;
        private int _activeReaders;
        private DateTime _lastConnection;
        private DateTime _lastActivity;

        // Constructeur privé pour forcer l'utilisation des méthodes factory
        private MockSQLManager(Func<string, RootDbContext> contextFactory, string folderPath, string fileName, string extension)
            : base(contextFactory, folderPath, fileName, extension)
        {
            _lastConnection = DateTime.Now;
            _lastActivity = DateTime.Now;
        }

        // Factory pour créer une base connectée
        public static MockSQLManager CreateConnectedDatabase(string fileName, string folderPath, string extension)
        {
            var mock = new MockSQLManager(
                contextFactory: (path) => null, // Context factory fictif
                folderPath: folderPath,
                fileName: fileName,
                extension: extension
            );

            mock._mockState = ConnectionState.Connected;

            // Simuler des événements de connexion
            Task.Run(async () =>
            {
                await Task.Delay(100);
                mock.SimulateConnectionStateChange(ConnectionState.Connected);

                // Simuler quelques opérations de lecture/écriture
                await mock.SimulateRandomActivity();
            });

            return mock;
        }

        // Factory pour créer une base déconnectée
        public static MockSQLManager CreateDisconnectedDatabase(string fileName, string folderPath, string extension)
        {
            var mock = new MockSQLManager(
                contextFactory: (path) => null,
                folderPath: folderPath,
                fileName: fileName,
                extension: extension
            );

            mock._mockState = ConnectionState.Disconnected;
            return mock;
        }

        // Simulation du changement d'état
        private void SimulateConnectionStateChange(ConnectionState newState)
        {
            _mockState = newState;
            // Déclencher l'événement ConnectionStateChanged
            OnConnectionStateChanged(this, newState);
        }

        // Simulation d'activité aléatoire
        private async Task SimulateRandomActivity()
        {
            var random = new Random();

            while (_mockState == ConnectionState.Connected)
            {
                await Task.Delay(random.Next(2000, 8000));

                // 50% chance de lecture
                if (random.Next(0, 2) == 0)
                {
                    SimulateReadOperation();
                }
                // 30% chance d'écriture
                else if (random.Next(0, 10) < 3)
                {
                    SimulateWriteOperation();
                }
            }
        }

        private async void SimulateReadOperation()
        {
            _isReading = true;
            _activeReaders++;
            _lastActivity = DateTime.Now;

            // Déclencher l'événement ReadOperationStarted
            OnReadOperationStarted(5 - _activeReaders);

            await Task.Delay(new Random().Next(500, 2000));

            _activeReaders--;
            _isReading = _activeReaders > 0;

            // Déclencher l'événement ReadOperationEnded
            OnReadOperationEnded(5 - _activeReaders);
        }

        private async void SimulateWriteOperation()
        {
            _isWriting = true;
            _lastActivity = DateTime.Now;

            // Déclencher l'événement WriteOperationStarted
            OnWriteOperationStarted();

            await Task.Delay(new Random().Next(800, 3000));

            _isWriting = false;

            // Déclencher l'événement WriteOperationEnded
            OnWriteOperationEnded();
        }

        // Méthodes protégées pour déclencher les événements
        protected virtual void OnConnectionStateChanged(object sender, ConnectionState state)
        {
            // Cette méthode sera appelée par la classe dérivée pour déclencher l'événement
            var handler = typeof(SQLManager)
                .GetEvent("ConnectionStateChanged")
                ?.GetRaiseMethod(true);

            if (handler != null)
            {
                handler.Invoke(this, new object[] { sender, state });
            }
        }

        protected virtual void OnReadOperationStarted(int remaining)
        {
            var handler = typeof(SQLManager)
                .GetField("ReadOperationStarted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(this) as Action<int>;

            handler?.Invoke(remaining);
        }

        protected virtual void OnReadOperationEnded(int remaining)
        {
            var handler = typeof(SQLManager)
                .GetField("ReadOperationEnded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(this) as Action<int>;

            handler?.Invoke(remaining);
        }

        protected virtual void OnWriteOperationStarted()
        {
            var handler = typeof(SQLManager)
                .GetField("WriteOperationStarted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(this) as Action;

            handler?.Invoke();
        }

        protected virtual void OnWriteOperationEnded()
        {
            var handler = typeof(SQLManager)
                .GetField("WriteOperationEnded", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(this) as Action;

            handler?.Invoke();
        }

        // Override des propriétés pour retourner des valeurs mockées
        public new ConnectionState CurrentState => _mockState;
        public new bool IsConnected => _mockState == ConnectionState.Connected;
        public new bool IsReading => _isReading;
        public new bool IsWriting => _isWriting;
        public new int ActiveReaders => _activeReaders;
        public new DateTime? LastConnection => _lastConnection;
        public new DateTime? LastActivity => _lastActivity;

        // Override des méthodes async pour retourner des résultats mockés
        public new Task<HealthCheckResult> CheckHealthAsync()
        {
            var result = new HealthCheckResult
            {
                Status = _mockState == ConnectionState.Connected ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Description = _mockState == ConnectionState.Connected ? "Base de données opérationnelle" : "Base de données déconnectée",
                Details = new Dictionary<string, object>
                {
                    ["pingMs"] = new Random().Next(10, 100),
                    ["integrityCheck"] = "OK",
                    ["state"] = _mockState.ToString()
                }
            };

            return Task.FromResult(result);
        }

        public new Task<HealthCheckResult> QuickHealthCheckAsync()
        {
            return CheckHealthAsync();
        }

        public new Task<Result<DatabaseStatistics>> GetDatabaseStatisticsAsync()
        {
            var stats = new DatabaseStatistics
            {
                PageCount = 320,
                PageSize = 4096,
                FreePageCount = 175,
                TotalSizeBytes = 320 * 4096,
                UsedSizeBytes = 145 * 4096,
                FreeSizeBytes = 175 * 4096,
                FileSizeBytes = 2621440,
                WalSizeBytes = 524288,
                TableCount = 15
            };

            return Task.FromResult(Result<DatabaseStatistics>.Success(stats));
        }

        public new Result<DatabaseFileInfo> GetDatabaseFileInfo()
        {
            var info = new DatabaseFileInfo
            {
                IsInMemory = false,
                FullPath = GetFullPath,
                MainFileExists = true,
                MainFileSize = 2621440,
                WalFileExists = _mockState == ConnectionState.Connected,
                WalFileSize = 524288,
                ShmFileExists = _mockState == ConnectionState.Connected,
                ShmFileSize = 32768,
                TotalSize = 3178496
            };

            return Result<DatabaseFileInfo>.Success(info);
        }

        public new Task<Result<Dictionary<string, string>>> GetConcurrencyConfigAsync()
        {
            var config = new Dictionary<string, string>
            {
                ["journal_mode"] = "WAL",
                ["busy_timeout"] = "5000",
                ["cache_size"] = "-20000",
                ["synchronous"] = "NORMAL",
                ["locking_mode"] = "NORMAL",
                ["foreign_keys"] = "1"
            };

            return Task.FromResult(Result<Dictionary<string, string>>.Success(config));
        }

        public new Task<Result> ConnectAsync()
        {
            SimulateConnectionStateChange(ConnectionState.Connected);
            _ = SimulateRandomActivity();
            return Task.FromResult(Result.Success("Connecté avec succès"));
        }

        public new Task<Result> DisconnectAsync()
        {
            SimulateConnectionStateChange(ConnectionState.Disconnected);
            return Task.FromResult(Result.Success("Déconnecté avec succès"));
        }

        public new Task<Result> FlushAsync()
        {
            _lastActivity = DateTime.Now;
            return Task.FromResult(Result.Success("Données synchronisées"));
        }
    }
}
