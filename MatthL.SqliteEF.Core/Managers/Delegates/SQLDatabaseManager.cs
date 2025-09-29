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
[assembly: InternalsVisibleTo("SQLiteManager.Tests")]
namespace MatthL.SqliteEF.Core.Managers.Delegates
{
    internal class SQLDatabaseManager
    {
        private RootDbContext _dbContext;
        private string _folderPath;
        private string _fileName;
        private readonly bool _inMemory;

        public string FullPath => Path.Combine(_folderPath, _fileName + ".db");
        public string FolderPath => _folderPath;
        public string FileName => _fileName;
        public long FileSize => CheckFileSize();
        public bool IsInMemory => _inMemory;
        private SQLConnectionManager _connectionManager;

        public SQLDatabaseManager(RootDbContext dbContext, SQLConnectionManager connectionManager, string folderPath = null, string fileName = null)
        {
            _dbContext = dbContext;
            _folderPath = folderPath;
            _fileName = fileName;
            _inMemory = string.IsNullOrEmpty(folderPath);
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// Met à jour le contexte de base de données (appelé par SQLManager lors d'un refresh)
        /// </summary>
        public void UpdateContext(RootDbContext newContext)
        {
            _dbContext = newContext;
        }

        public void SetPaths(string folderPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentNullException(nameof(folderPath));
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            _folderPath = folderPath;
            _fileName = Path.GetFileNameWithoutExtension(fileName);

            _dbContext.DatabasePath = _inMemory ? ":memory:" : FullPath;
        }
        public void SetPaths()
        {
            if (!_inMemory && (string.IsNullOrWhiteSpace(_folderPath) || string.IsNullOrWhiteSpace(_fileName)))
                throw new ArgumentNullException(nameof(_folderPath));

            if (_inMemory)
            {
                _dbContext.DatabasePath = ":memory:";
                return;
            }

            if (_fileName.Split('.').Length > 0)
            {
                _fileName = _fileName.Split('.').First();
            }

            _dbContext.DatabasePath = FullPath;
        }
        private long CheckFileSize()
        {
            if (_inMemory) { return -1; }
            if(File.Exists(FullPath))
            {
                var fileinfo = new FileInfo(FullPath);
                return fileinfo.Length;
            }
            return 0;
        }
        public async Task<Result> CreateAsync()
        {
            try
            {
                if (_dbContext == null)
                    return Result.Failure("No db context available");

                if (!_inMemory)
                {
                    Directory.CreateDirectory(_folderPath);
                }

                string filePath = FullPath;

                if (!_inMemory && File.Exists(filePath))
                {
                    var result = await _connectionManager.ConnectAsync();
                    await _dbContext.Database.MigrateAsync();
                    return result.IsSuccess
                        ? Result.Success("Database created and migrated")
                        : Result.Failure("Ensure Created Failed");
                }
                else
                {
                    var created = await _dbContext.Database.EnsureCreatedAsync();
                    return created
                        ? Result.Success("Database created")
                        : Result.Failure("Ensure Created Failed");
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
                string filePath = _dbContext?.DatabasePath;

                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return Result.Failure($"Path is incorrect or file doesn't exist at {filePath}");

                services.Clear();
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
            if (_dbContext != null)
            {
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE)");
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA optimize");
                await _dbContext.Database.CloseConnectionAsync();
                await _connectionManager.ChangeStateAsync(Enums.ConnectionState.Disconnected);
            }

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
                    return Result.Failure($"Path is incorrect or file doesn't exist at {filePath}");

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

                        return Result.Success();
                    }
                    catch (IOException)
                    {
                        if (i == maxRetries - 1)
                            throw;

                        await Task.Delay(200 * (i + 1)); // Attente progressive
                    }
                }

                return Result.Failure("Failed to delete the database");
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    return Result.Failure($"Database Deletion failed {ex.InnerException.Message}");
                return Result.Failure($"Database Deletion failed {ex.Message}");
            }
        }
    }
}
