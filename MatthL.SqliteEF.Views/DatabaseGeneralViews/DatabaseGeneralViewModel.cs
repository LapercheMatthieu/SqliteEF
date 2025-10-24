using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Tools;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MatthL.SqliteEF.Views.DatabaseGeneralViews
{
    public partial class DatabaseGeneralViewModel : ObservableObject, IDisposable
    {
        private readonly SQLManager _manager;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private string _databaseName;

        [ObservableProperty]
        private string _folderPath;

        [ObservableProperty]
        private string _fullPath;

        [ObservableProperty]
        private string _extension;

        [ObservableProperty]
        private string _sizeString;

        [ObservableProperty]
        private string _creationDate;

        [ObservableProperty]
        private string _lastModificationDate;

        [ObservableProperty]
        private ConnectionState _connectionState;

        [ObservableProperty]
        private bool _isInMemory;

        public SQLManager Manager => _manager;

        public DatabaseGeneralViewModel(SQLManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));

            // Abonnement aux événements
            _manager.ConnectionStateChanged += OnConnectionStateChanged;

            // Initialisation
            RefreshView();
        }

        private void OnConnectionStateChanged(object sender, ConnectionState state)
        {
            ConnectionState = state;
            IsConnected = state == ConnectionState.Connected;
            RefreshView();
        }

        private void RefreshView()
        {
            // Initialiser l'état de connexion
            ConnectionState = _manager.CurrentState;
            IsConnected = _manager.IsConnected;
            IsInMemory = _manager.IsInMemory;

            if (_manager.IsConnected && !IsInMemory)
            {
                DatabaseName = _manager.GetFileName;
                FolderPath = _manager.GetFolderPath;
                FullPath = _manager.GetFullPath;
                Extension = _manager.GetFileExtension;
                SizeString = _manager.GetFileSize.ToFileSizeString();

                if (File.Exists(FullPath))
                {
                    var fileInfo = new FileInfo(FullPath);
                    CreationDate = fileInfo.CreationTime.ToString("dd MMM yyyy 'à' HH:mm");
                    LastModificationDate = fileInfo.LastWriteTime.ToString("dd MMM yyyy 'à' HH:mm");
                }
            }
            else if (_manager.IsConnected && IsInMemory)
            {
                DatabaseName = "Base en mémoire";
                FolderPath = "N/A";
                FullPath = ":memory:";
                Extension = "N/A";
                SizeString = "N/A";
                CreationDate = "N/A";
                LastModificationDate = "N/A";
            }
            else
            {
                DatabaseName = "Non connecté";
                FolderPath = string.Empty;
                FullPath = string.Empty;
                Extension = string.Empty;
                SizeString = "0 B";
                CreationDate = string.Empty;
                LastModificationDate = string.Empty;
            }
        }

        [RelayCommand]
        private async Task SelectDatabaseAsync()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Database files (*.db;*.sqlite;*.sqlite3)|*.db;*.sqlite;*.sqlite3|All files (*.*)|*.*",
                    Title = "Sélectionner une base de données"
                };

                if (dialog.ShowDialog() == true)
                {
                    var folderPath = Path.GetDirectoryName(dialog.FileName);
                    var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                    var extension = Path.GetExtension(dialog.FileName);

                    _manager.SetPaths(folderPath, fileName, extension);
                    var result = await _manager.ConnectAsync();

                    if (result.IsFailure)
                    {
                        MessageBox.Show($"Échec de la connexion : {result.Error}", "Erreur",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la sélection : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CreateDatabaseAsync()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Database files (*.db)|*.db|SQLite files (*.sqlite)|*.sqlite|All files (*.*)|*.*",
                    Title = "Créer une nouvelle base de données",
                    DefaultExt = ".db"
                };

                if (dialog.ShowDialog() == true)
                {
                    var folderPath = Path.GetDirectoryName(dialog.FileName);
                    var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                    var extension = Path.GetExtension(dialog.FileName);

                    _manager.SetPaths(folderPath, fileName, extension);
                    var result = await _manager.Create();

                    if (result.IsSuccess)
                    {
                        MessageBox.Show("Base de données créée avec succès!", "Succès",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Échec de la création : {result.Error}", "Erreur",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la création : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteDatabaseAsync()
        {
            try
            {
                if (!_manager.IsConnected)
                {
                    MessageBox.Show("Aucune base de données connectée.", "Information",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Êtes-vous sûr de vouloir supprimer définitivement la base de données '{DatabaseName}' ?\n\nCette action est irréversible!",
                    "Confirmation de suppression",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var deleteResult = await _manager.DeleteCurrentDatabase();

                    if (deleteResult.IsSuccess)
                    {
                        MessageBox.Show("Base de données supprimée avec succès.", "Succès",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Échec de la suppression : {deleteResult.Error}", "Erreur",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la suppression : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenFolder()
        {
            try
            {
                if (string.IsNullOrEmpty(FolderPath) || !Directory.Exists(FolderPath))
                {
                    MessageBox.Show("Le dossier n'existe pas ou n'est pas défini.", "Information",
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
        private void ShowDetails()
        {
            // Événement pour demander l'ouverture de la vue détaillée
            DetailsRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            try
            {
                var result = await _manager.ConnectAsync();

                if (result.IsFailure)
                {
                    MessageBox.Show($"Échec de la connexion : {result.Error}", "Erreur",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la connexion : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DisconnectAsync()
        {
            try
            {
                var result = await _manager.DisconnectAsync();

                if (result.IsFailure)
                {
                    MessageBox.Show($"Échec de la déconnexion : {result.Error}", "Erreur",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la déconnexion : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event EventHandler DetailsRequested;

        public void Dispose()
        {
            _manager.ConnectionStateChanged -= OnConnectionStateChanged;
        }
    }
}
