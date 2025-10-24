using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MatthL.SqliteEF.Views.DatabaseDetailViews
{
    public partial class DatabaseDetailViewModel : ObservableObject, IDisposable
    {
        private readonly SQLManager _manager;
        private readonly DispatcherTimer _refreshTimer;

        #region Observable Properties - General Info

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private string _databaseName;

        [ObservableProperty]
        private string _fullPath;

        [ObservableProperty]
        private string _folderPath;

        [ObservableProperty]
        private string _extension;

        [ObservableProperty]
        private ConnectionState _connectionState;

        [ObservableProperty]
        private bool _isInMemory;

        #endregion

        #region Observable Properties - File Info

        [ObservableProperty]
        private string _mainFileSize;

        [ObservableProperty]
        private string _walFileSize;

        [ObservableProperty]
        private string _shmFileSize;

        [ObservableProperty]
        private string _totalFileSize;

        [ObservableProperty]
        private bool _walFileExists;

        [ObservableProperty]
        private bool _shmFileExists;

        [ObservableProperty]
        private string _creationDate;

        [ObservableProperty]
        private string _lastModificationDate;

        [ObservableProperty]
        private string _lastAccessDate;

        #endregion

        #region Observable Properties - Health Check

        [ObservableProperty]
        private HealthStatus _healthStatus;

        [ObservableProperty]
        private string _healthDescription;

        [ObservableProperty]
        private long _pingMs;

        [ObservableProperty]
        private string _integrityCheck;

        [ObservableProperty]
        private DateTime? _lastHealthCheck;

        #endregion

        #region Observable Properties - Statistics

        [ObservableProperty]
        private long _pageCount;

        [ObservableProperty]
        private long _pageSize;

        [ObservableProperty]
        private long _freePageCount;

        [ObservableProperty]
        private string _totalSize;

        [ObservableProperty]
        private string _usedSize;

        [ObservableProperty]
        private string _freeSize;

        [ObservableProperty]
        private double _usagePercentage;

        [ObservableProperty]
        private int _tableCount;

        #endregion

        #region Observable Properties - Connection Info

        [ObservableProperty]
        private DateTime? _lastConnection;

        [ObservableProperty]
        private DateTime? _lastActivity;

        [ObservableProperty]
        private string _journalMode;

        [ObservableProperty]
        private string _busyTimeout;

        [ObservableProperty]
        private string _cacheSize;

        [ObservableProperty]
        private string _synchronous;

        [ObservableProperty]
        private string _lockingMode;

        [ObservableProperty]
        private string _foreignKeys;

        #endregion

        #region Observable Properties - Activity

        [ObservableProperty]
        private bool _isReading;

        [ObservableProperty]
        private bool _isWriting;

        [ObservableProperty]
        private int _activeReaders;

        [ObservableProperty]
        private bool _isRefreshing;

        #endregion

        public SQLManager Manager => _manager;

        public DatabaseDetailViewModel(SQLManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));

            // Abonnement aux événements
            _manager.ConnectionStateChanged += OnConnectionStateChanged;
            _manager.ReadOperationStarted += OnReadOperationStarted;
            _manager.ReadOperationEnded += OnReadOperationEnded;
            _manager.WriteOperationStarted += OnWriteOperationStarted;
            _manager.WriteOperationEnded += OnWriteOperationEnded;

            // Timer pour rafraîchir les données
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshAllDataAsync();

            // Initialisation
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await RefreshAllDataAsync();

            if (_manager.IsConnected)
            {
                _refreshTimer.Start();
            }
        }

        private void OnConnectionStateChanged(object sender, ConnectionState state)
        {
            ConnectionState = state;
            IsConnected = state == ConnectionState.Connected;

            if (IsConnected)
            {
                _ = RefreshAllDataAsync();
                _refreshTimer.Start();
            }
            else
            {
                _refreshTimer.Stop();
                ClearData();
            }
        }

        private void OnReadOperationStarted(int remaining)
        {
            IsReading = true;
            ActiveReaders = _manager.ActiveReaders;
        }

        private void OnReadOperationEnded(int remaining)
        {
            IsReading = _manager.IsReading;
            ActiveReaders = _manager.ActiveReaders;
        }

        private void OnWriteOperationStarted()
        {
            IsWriting = true;
        }

        private void OnWriteOperationEnded()
        {
            IsWriting = _manager.IsWriting;
        }

        [RelayCommand]
        private async Task RefreshAllDataAsync()
        {
            if (IsRefreshing) return;

            try
            {
                IsRefreshing = true;

                await RefreshGeneralInfoAsync();
                await RefreshFileInfoAsync();
                await RefreshHealthCheckAsync();
                await RefreshStatisticsAsync();
                await RefreshConnectionInfoAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task RefreshGeneralInfoAsync()
        {
            // Initialiser l'état de connexion
            ConnectionState = _manager.CurrentState;
            IsConnected = _manager.IsConnected;
            IsInMemory = _manager.IsInMemory;

            if (_manager.IsConnected)
            {
                DatabaseName = _manager.GetFileName;
                FullPath = _manager.GetFullPath;
                FolderPath = _manager.GetFolderPath;
                Extension = _manager.GetFileExtension;
                LastConnection = _manager.LastConnection;
                LastActivity = _manager.LastActivity;
            }
        }

        private async Task RefreshFileInfoAsync()
        {
            if (_manager.IsConnected && !IsInMemory)
            {
                var fileInfoResult = _manager.GetDatabaseFileInfo();

                if (fileInfoResult.IsSuccess)
                {
                    var info = fileInfoResult.Value;
                    MainFileSize = info.MainFileSizeString;
                    WalFileSize = info.WalFileSizeString;
                    ShmFileSize = info.ShmFileSizeString;
                    TotalFileSize = info.TotalSizeString;
                    WalFileExists = info.WalFileExists;
                    ShmFileExists = info.ShmFileExists;
                }

                if (File.Exists(FullPath))
                {
                    var fileInfo = new FileInfo(FullPath);
                    CreationDate = fileInfo.CreationTime.ToString("dd MMM yyyy 'à' HH:mm:ss");
                    LastModificationDate = fileInfo.LastWriteTime.ToString("dd MMM yyyy 'à' HH:mm:ss");
                    LastAccessDate = fileInfo.LastAccessTime.ToString("dd MMM yyyy 'à' HH:mm:ss");
                }
            }
        }

        private async Task RefreshHealthCheckAsync()
        {
            if (_manager.IsConnected)
            {
                var healthResult = await _manager.CheckHealthAsync();

                if (healthResult != null)
                {
                    HealthStatus = healthResult.Status;
                    HealthDescription = healthResult.Description;
                    LastHealthCheck = DateTime.Now;

                    if (healthResult.Details.ContainsKey("pingMs"))
                    {
                        PingMs = Convert.ToInt64(healthResult.Details["pingMs"]);
                    }

                    if (healthResult.Details.ContainsKey("integrityCheck"))
                    {
                        IntegrityCheck = healthResult.Details["integrityCheck"].ToString();
                    }
                }
            }
        }

        private async Task RefreshStatisticsAsync()
        {
            if (_manager.IsConnected)
            {
                var statsResult = await _manager.GetDatabaseStatisticsAsync();

                if (statsResult.IsSuccess)
                {
                    var stats = statsResult.Value;
                    PageCount = stats.PageCount;
                    PageSize = stats.PageSize;
                    FreePageCount = stats.FreePageCount;
                    TotalSize = stats.TotalSizeString;
                    UsedSize = stats.UsedSizeString;
                    FreeSize = stats.FreeSizeString;
                    UsagePercentage = stats.UsagePercentage;
                    TableCount = stats.TableCount;
                }
            }
        }

        private async Task RefreshConnectionInfoAsync()
        {
            if (_manager.IsConnected)
            {
                var configResult = await _manager.GetConcurrencyConfigAsync();

                if (configResult.IsSuccess)
                {
                    var config = configResult.Value;
                    JournalMode = config.GetValueOrDefault("journal_mode", "unknown");
                    BusyTimeout = config.GetValueOrDefault("busy_timeout", "unknown");
                    CacheSize = config.GetValueOrDefault("cache_size", "unknown");
                    Synchronous = config.GetValueOrDefault("synchronous", "unknown");
                    LockingMode = config.GetValueOrDefault("locking_mode", "unknown");
                    ForeignKeys = config.GetValueOrDefault("foreign_keys", "unknown");
                }
            }
        }

        private void ClearData()
        {
            DatabaseName = "Non connecté";
            FullPath = string.Empty;
            FolderPath = string.Empty;
            Extension = string.Empty;
            MainFileSize = "0 B";
            WalFileSize = "0 B";
            ShmFileSize = "0 B";
            TotalFileSize = "0 B";
            WalFileExists = false;
            ShmFileExists = false;
            HealthStatus = HealthStatus.Unhealthy;
            HealthDescription = "Base de données déconnectée";
            IntegrityCheck = "N/A";
            PageCount = 0;
            TableCount = 0;
            UsagePercentage = 0;
        }

        [RelayCommand]
        private void OpenFolder()
        {
            try
            {
                if (string.IsNullOrEmpty(FolderPath) || !Directory.Exists(FolderPath))
                {
                    MessageBox.Show("Le dossier n'existe pas.", "Information",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Process.Start("explorer.exe", FolderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'ouvrir le dossier : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task FlushAsync()
        {
            try
            {
                var result = await _manager.FlushAsync();

                if (result.IsSuccess)
                {
                    MessageBox.Show("Données synchronisées avec succès.", "Succès",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshAllDataAsync();
                }
                else
                {
                    MessageBox.Show($"Échec de la synchronisation : {result.Error}", "Erreur",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CheckIntegrityAsync()
        {
            try
            {
                await RefreshHealthCheckAsync();

                MessageBox.Show(
                    $"Vérification d'intégrité terminée.\n\nRésultat : {IntegrityCheck}",
                    "Vérification d'intégrité",
                    MessageBoxButton.OK,
                    IntegrityCheck == "OK" ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la vérification : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();

            _manager.ConnectionStateChanged -= OnConnectionStateChanged;
            _manager.ReadOperationStarted -= OnReadOperationStarted;
            _manager.ReadOperationEnded -= OnReadOperationEnded;
            _manager.WriteOperationStarted -= OnWriteOperationStarted;
            _manager.WriteOperationEnded -= OnWriteOperationEnded;
        }
    }
}
