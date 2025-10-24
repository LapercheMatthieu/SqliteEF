using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MatthL.SqliteEF.Views.DatabaseCompactViews
{
    public partial class DatabaseCompactViewModel : ObservableObject, IDisposable
    {
        private readonly SQLManager _manager;
        private readonly DispatcherTimer _updateTimer;

        [ObservableProperty]
        private string _databaseName;

        [ObservableProperty]
        private ConnectionState _connectionState;

        [ObservableProperty]
        private HealthStatus _healthStatus;

        [ObservableProperty]
        private bool _isReading;

        [ObservableProperty]
        private bool _isWriting;

        [ObservableProperty]
        private int _activeReaders;

        [ObservableProperty]
        private bool _hasActivity;

        public SQLManager Manager => _manager;

        public DatabaseCompactViewModel(SQLManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));

            // Initialisation
            DatabaseName = string.IsNullOrEmpty(_manager.GetFileName) ? "Non connecté" : _manager.GetFileName;
            ConnectionState = _manager.CurrentState;
            HealthStatus = HealthStatus.Unhealthy;

            // Abonnement aux événements
            _manager.ConnectionStateChanged += OnConnectionStateChanged;
            _manager.ReadOperationStarted += OnReadOperationStarted;
            _manager.ReadOperationEnded += OnReadOperationEnded;
            _manager.WriteOperationStarted += OnWriteOperationStarted;
            _manager.WriteOperationEnded += OnWriteOperationEnded;

            // Timer pour mettre à jour le health status périodiquement
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _updateTimer.Tick += async (s, e) => await UpdateHealthStatusAsync();
            _updateTimer.Start();
        }

        private void OnConnectionStateChanged(object sender, ConnectionState state)
        {
            ConnectionState = state;
            DatabaseName = string.IsNullOrEmpty(_manager.GetFileName) ? "Non connecté" : _manager.GetFileName;

            if (state == ConnectionState.Connected)
            {
                _ = UpdateHealthStatusAsync();
            }
            else
            {
                HealthStatus = HealthStatus.Unhealthy;
            }
        }

        private void OnReadOperationStarted(int remaining)
        {
            IsReading = true;
            ActiveReaders = _manager.ActiveReaders;
            HasActivity = true;
        }

        private void OnReadOperationEnded(int remaining)
        {
            IsReading = _manager.IsReading;
            ActiveReaders = _manager.ActiveReaders;
        }

        private void OnWriteOperationStarted()
        {
            IsWriting = true;
            HasActivity = true;
        }

        private void OnWriteOperationEnded()
        {
            IsWriting = _manager.IsWriting;
        }

        private async System.Threading.Tasks.Task UpdateHealthStatusAsync()
        {
            if (_manager.IsConnected)
            {
                var healthResult = await _manager.QuickHealthCheckAsync();
                if (healthResult != null)
                {
                    HealthStatus = healthResult.Status;
                }
            }
        }

        [RelayCommand]
        private void ShowDetails()
        {
            // Événement pour demander l'ouverture de la vue détaillée
            DetailsRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler DetailsRequested;

        public void Dispose()
        {
            _updateTimer?.Stop();

            _manager.ConnectionStateChanged -= OnConnectionStateChanged;
            _manager.ReadOperationStarted -= OnReadOperationStarted;
            _manager.ReadOperationEnded -= OnReadOperationEnded;
            _manager.WriteOperationStarted -= OnWriteOperationStarted;
            _manager.WriteOperationEnded -= OnWriteOperationEnded;
        }
    }
}
