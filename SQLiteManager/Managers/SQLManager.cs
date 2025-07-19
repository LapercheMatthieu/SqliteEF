using Microsoft.EntityFrameworkCore;
using SQLiteManager.Authorizations;
using SQLiteManager.Models;
using SQLiteManager.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SQLiteManager.Managers
{
    /// <summary>
    /// Gestionnaire central pour les opérations de base de données SQLite.
    /// Sert de façade pour toutes les fonctionnalités de la bibliothèque.
    /// </summary>
    public class SQLManager
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private RootDbContext _dbContext;
        private string _fileName;
        private string _folderPath;
        private IAuthorizationManager authorizationManager;

        public SQLManager(RootDbContext dbContext, string FolderPath, string FileName, IAuthorizationManager _authorizationManager)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            authorizationManager = _authorizationManager;
            _services.Clear();
            SetPaths(FolderPath, FileName);
        }
        /// <summary>
        /// Fabrique in memory
        /// </summary>
        /// <param name="dbContext"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public SQLManager(RootDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            authorizationManager = new AdminAuthorization();
            _services.Clear();

            _dbContext.DatabasePath = ":memory:";
        }

        public void SetPaths(string folderPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(folderPath));

            _folderPath = folderPath;

            if(fileName.Split('.').Length > 0)
            {
                _fileName = fileName.Split('.').First();
            }
            else
            {
                _fileName = fileName;
            }
            _dbContext.DatabasePath = Path.Combine(_folderPath, _fileName + ".db");
        }


        /// <summary>
        /// Crée une nouvelle base de données SQLite dans le dossier spécifié
        /// </summary>
        /// <returns>True si la création a réussi, False sinon</returns>
        public async Task<bool> Create()
        {
            try
            {
                
                // Assurer que le dossier existe
                Directory.CreateDirectory(_folderPath);

                // Construire le chemin complet du fichier
                string filePath = Path.Combine(_folderPath, $"{_fileName}.db");

                // Vérifier si le fichier existe déjà

                try
                {
                    /*if (!File.Exists(filePath))
                    {
                        using (var fs = new FileStream(
                        filePath,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.None))

                        {
                            // Juste créer le fichier
                            fs.Close();
                        }
                    }*/

                    // Fermer toutes les connexions avant de supprimer
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    Console.WriteLine("Fichier créé manuellement avec succès");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors de la création manuelle: {ex.Message}");
                }

                // Créer un fichier vide pour la base de données
               var success = await _dbContext.Database.EnsureCreatedAsync();

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la création de la base de données: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Supprime une base de données SQLite existante
        /// </summary>
        /// <param name="filePath">Chemin complet vers le fichier de base de données</param>
        /// <returns>True si la suppression a réussi, False sinon</returns>
        public async Task<bool> Delete(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return false;

                // Fermer les connexions à la base de données
                if (_dbContext != null)
                {
                    await _dbContext.DisposeAsync();
                    _dbContext = null;
                }

                // Supprimer le fichier principal
                File.Delete(filePath);

                // Supprimer les fichiers associés (WAL, SHM) s'ils existent
                string walPath = $"{filePath}-wal";
                string shmPath = $"{filePath}-shm";

                if (File.Exists(walPath))
                    File.Delete(walPath);

                if (File.Exists(shmPath))
                    File.Delete(shmPath);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la suppression de la base de données: {ex.Message}");
                return false;
            }
        }



        /// <summary>
        /// Obtient un service pour le type d'entité spécifié
        /// </summary>
        /// <typeparam name="T">Type d'entité</typeparam>
        /// <returns>Service pour le type d'entité</returns>
        public IService<T> GetService<T>() where T : class, IBaseEntity
        {
            if (_dbContext == null)
                throw new InvalidOperationException("Le gestionnaire n'a pas été initialisé. Appelez Initialize d'abord.");

            var entityType = typeof(T);

            if (!_services.TryGetValue(entityType, out var service))
            {
                service = new BaseService<T>(_dbContext,authorizationManager);
                _services[entityType] = service;
            }

            return (IService<T>)service;
        }

        #region Méthodes de commodité pour les opérations CRUD

        /// <summary>
        /// Ajoute une entité à la base de données
        /// </summary>
        public async Task<bool> AddAsync<T>(T entity) where T : class, IBaseEntity
        {
            return await GetService<T>().AddAsync(entity);
        }

        /// <summary>
        /// Ajoute une liste d'entités à la base de données
        /// </summary>
        public async Task<bool> AddRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            return await GetService<T>().AddListAsync(entities);
        }

        /// <summary>
        /// Met à jour une entité dans la base de données
        /// </summary>
        public async Task<bool> UpdateAsync<T>(T entity) where T : class, IBaseEntity
        {
            return await GetService<T>().UpdateAsync(entity);
        }

        /// <summary>
        /// Met à jour une liste d'entités dans la base de données
        /// </summary>
        public async Task<bool> UpdateRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            return await GetService<T>().UpdateListAsync(entities);
        }

        /// <summary>
        /// Ajoute ou met à jour une entité selon qu'elle existe déjà ou non
        /// </summary>
        public async Task<bool> AddOrUpdateAsync<T>(T entity) where T : class, IBaseEntity
        {
            return await GetService<T>().AddOrUpdateAsync(entity);
        }

        /// <summary>
        /// Supprime une entité de la base de données
        /// </summary>
        public async Task<bool> DeleteAsync<T>(T entity) where T : class, IBaseEntity
        {
            return await GetService<T>().DeleteAsync(entity);
        }

        /// <summary>
        /// Supprime une liste d'entités de la base de données
        /// </summary>
        public async Task<bool> DeleteRangeAsync<T>(IEnumerable<T> entities) where T : class, IBaseEntity
        {
            return await GetService<T>().DeleteListAsync(entities);
        }

        /// <summary>
        /// Supprime toutes les entités d'un type spécifique
        /// </summary>
        public async Task<bool> DeleteAllAsync<T>() where T : class, IBaseEntity
        {
            return await GetService<T>().DeleteAllAsync();
        }

        /// <summary>
        /// Récupère toutes les entités d'un type spécifique
        /// </summary>
        public async Task<List<T>> GetAllAsync<T>() where T : class, IBaseEntity
        {
            return await GetService<T>().GetAllAsync();
        }

        /// <summary>
        /// Récupère une entité par son ID
        /// </summary>
        public async Task<T> GetByIdAsync<T>(int id) where T : class, IBaseEntity
        {
            return await GetService<T>().GetItem(id);
        }

        /// <summary>
        /// Vérifie si des entités d'un type spécifique existent
        /// </summary>
        public async Task<bool> AnyExistAsync<T>() where T : class, IBaseEntity
        {
            return await GetService<T>().AnyExist();
        }

        /// <summary>
        /// Vérifie si une entité est sauvegardable (tous les champs requis sont remplis)
        /// </summary>
        public bool IsSavable<T>(T entity) where T : class, IBaseEntity
        {
            return GetService<T>().IsSavable(entity);
        }

        #endregion

        /// <summary>
        /// Libère les ressources utilisées par le gestionnaire
        /// </summary>
        public async Task DisposeAsync()
        {
            if (_dbContext != null)
            {
                await _dbContext.Database.CloseConnectionAsync();
                await _dbContext.DisposeAsync();
                _dbContext = null;
            }
            _services.Clear();

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public async Task ConnectAsync()
        {
            if (_dbContext != null)
            {
                await _dbContext.Database.OpenConnectionAsync();
            }
        }
        public async Task CloseConnection()
        {
            if (_dbContext != null)
            {
                _services.Clear();
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL)");
                
                await _dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}