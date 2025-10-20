using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MatthL.SqliteEF.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Tools;
[assembly: InternalsVisibleTo("SQLiteManager.Tests")]
namespace MatthL.SqliteEF.Core.Managers.Delegates
{
    internal class SQLDatabaseManager
    {
        private readonly IDbContextFactory<RootDbContext> _contextFactory;
        private string _folderPath;
        private string _fileName;
        private string _fileExtension;
        private readonly bool _inMemory;
        private readonly SQLConnectionManager _connectionManager;

        public string FullPath => _inMemory ? ":memory:" : Path.Combine(_folderPath, _fileName + _fileExtension);
        public string FolderPath => _folderPath;
        public string FileName => _fileName;
        public string FileExtension => _fileExtension;
        public long FileSize => CheckFileSize();
        public bool IsInMemory => _inMemory;

        public SQLDatabaseManager(
            IDbContextFactory<RootDbContext> contextFactory,
            SQLConnectionManager connectionManager,
            string folderPath = null,
            string fileName = null,
            string extension = ".db")
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));

            _folderPath = folderPath;
            _fileName = fileName;
            _fileExtension = extension?.StartsWith(".") == true ? extension : $".{extension}";
            _inMemory = string.IsNullOrEmpty(folderPath);
        }

        public void SetPaths(string folderPath, string fileName, string extension = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentNullException(nameof(folderPath));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            _folderPath = folderPath;

            // Extraire le nom du fichier sans extension s'il en a une
            _fileName = Path.GetFileNameWithoutExtension(fileName);

            // Gérer l'extension
            if (!string.IsNullOrWhiteSpace(extension))
            {
                _fileExtension = extension.StartsWith(".") ? extension : $".{extension}";
            }
            else if (Path.HasExtension(fileName))
            {
                _fileExtension = Path.GetExtension(fileName);
            }
            // Sinon, garder l'extension par défaut définie dans le constructeur
        }

        public void SetPaths()
        {
            if (!_inMemory && (string.IsNullOrWhiteSpace(_folderPath) || string.IsNullOrWhiteSpace(_fileName)))
                throw new InvalidOperationException("Folder path and file name must be set for non-memory databases");

            // Les chemins sont déjà configurés, cette méthode valide juste qu'ils sont corrects
        }

        public void SetExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                throw new ArgumentNullException(nameof(extension));

            _fileExtension = extension.StartsWith(".") ? extension : $".{extension}";
        }

        private long CheckFileSize()
        {
            if (_inMemory)
                return -1;

            if (File.Exists(FullPath))
            {
                var fileInfo = new FileInfo(FullPath);
                return fileInfo.Length;
            }

            return 0;
        }

        public async Task<Result> CreateAsync()
        {
            try
            {
                if (!_inMemory)
                {
                    Directory.CreateDirectory(_folderPath);
                }

                string filePath = FullPath;

                await using var context = await _contextFactory.CreateDbContextAsync();

                if (!_inMemory && File.Exists(filePath))
                {
                    // Le fichier existe déjà, on applique les migrations
                    var connectResult = await _connectionManager.ConnectAsync();

                    if (connectResult.IsFailure)
                        return connectResult;

                    await context.Database.MigrateAsync();
                    return Result.Success("Database migrated successfully");
                }
                else
                {
                    // Créer une nouvelle base de données
                    var created = await context.Database.EnsureCreatedAsync();

                    if (created)
                    {
                        // Après création, établir la connexion
                        var connectResult = await _connectionManager.ConnectAsync();

                        return connectResult.IsSuccess
                            ? Result.Success("Database created successfully")
                            : Result.Failure($"Database created but connection failed: {connectResult.Error}");
                    }

                    return Result.Failure("Database creation failed");
                }
            }
            catch (Exception ex)
            {
                return Result.Failure($"Database creation failed: {ex.Message}");
            }
        }

        public async Task<Result> DeleteCurrentDatabaseAsync(IDictionary<Type, object> services)
        {
            try
            {
                if (_inMemory)
                    return Result.Failure("Cannot delete in-memory database");

                string filePath = FullPath;

                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return Result.Failure($"Database file doesn't exist at {filePath}");

                services?.Clear();
                await ForceCloseConnections();

                return await DeleteDatabaseFileStatic(filePath);
            }
            catch (Exception ex)
            {
                return Result.Failure($"Database deletion failed: {ex.Message}");
            }
            finally
            {
                await _connectionManager.ChangeStateAsync(Enums.ConnectionState.Disposed);
            }
        }

        private async Task ForceCloseConnections()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();

                await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)");
                await context.Database.ExecuteSqlRawAsync("PRAGMA optimize");
                await context.Database.CloseConnectionAsync();
            }
            catch
            {
                // Ignorer les erreurs lors de la fermeture forcée
            }

            await _connectionManager.DisconnectAsync();
            SqliteConnection.ClearAllPools();

            await Task.Delay(100);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        public static async Task<Result> DeleteDatabaseFileStatic(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return Result.Failure($"Database file doesn't exist at {filePath}");

                // Vider le pool de connexions SQLite globalement
                SqliteConnection.ClearAllPools();

                // Attendre un peu pour s'assurer que tout est libéré
                await Task.Delay(100);

                // Forcer le garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Essayer de supprimer le fichier plusieurs fois
                int maxRetries = 5;
                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        // Supprimer le fichier principal
                        if (File.Exists(filePath))
                            File.Delete(filePath);

                        // Supprimer les fichiers associés (WAL, SHM) s'ils existent
                        string walPath = $"{filePath}-wal";
                        string shmPath = $"{filePath}-shm";
                        string journalPath = $"{filePath}-journal";

                        if (File.Exists(walPath))
                            File.Delete(walPath);

                        if (File.Exists(shmPath))
                            File.Delete(shmPath);

                        if (File.Exists(journalPath))
                            File.Delete(journalPath);

                        return Result.Success("Database deleted successfully");
                    }
                    catch (IOException)
                    {
                        if (i == maxRetries - 1)
                            throw;

                        await Task.Delay(200 * (i + 1)); // Attente progressive
                    }
                }

                return Result.Failure("Failed to delete the database after multiple attempts");
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    return Result.Failure($"Database deletion failed: {ex.InnerException.Message}");

                return Result.Failure($"Database deletion failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get database file information including WAL and SHM files
        /// </summary>
        public Result<DatabaseFileInfo> GetDatabaseFileInfo()
        {
            try
            {
                if (_inMemory)
                {
                    return Result<DatabaseFileInfo>.Success(new DatabaseFileInfo
                    {
                        IsInMemory = true,
                        MainFileExists = false,
                        TotalSize = 0
                    });
                }

                var info = new DatabaseFileInfo
                {
                    IsInMemory = false,
                    FullPath = FullPath,
                    MainFileExists = File.Exists(FullPath)
                };

                if (info.MainFileExists)
                {
                    var mainFile = new FileInfo(FullPath);
                    info.MainFileSize = mainFile.Length;

                    string walPath = $"{FullPath}-wal";
                    if (File.Exists(walPath))
                    {
                        var walFile = new FileInfo(walPath);
                        info.WalFileSize = walFile.Length;
                        info.WalFileExists = true;
                    }

                    string shmPath = $"{FullPath}-shm";
                    if (File.Exists(shmPath))
                    {
                        var shmFile = new FileInfo(shmPath);
                        info.ShmFileSize = shmFile.Length;
                        info.ShmFileExists = true;
                    }

                    info.TotalSize = info.MainFileSize + info.WalFileSize + info.ShmFileSize;
                }

                return Result<DatabaseFileInfo>.Success(info);
            }
            catch (Exception ex)
            {
                return Result<DatabaseFileInfo>.Failure($"Failed to get database file info: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Information about database files
    /// </summary>
    public class DatabaseFileInfo
    {
        public bool IsInMemory { get; set; }
        public string FullPath { get; set; }
        public bool MainFileExists { get; set; }
        public long MainFileSize { get; set; }
        public bool WalFileExists { get; set; }
        public long WalFileSize { get; set; }
        public bool ShmFileExists { get; set; }
        public long ShmFileSize { get; set; }
        public long TotalSize { get; set; }

        public string MainFileSizeString => MainFileSize.ToFileSizeString();
        public string WalFileSizeString => WalFileSize.ToFileSizeString();
        public string ShmFileSizeString => ShmFileSize.ToFileSizeString();
        public string TotalSizeString => TotalSize.ToFileSizeString();
    }
}
