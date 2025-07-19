using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NexusAIO.Features.DatabaseGroup.Models
{
    /// <summary>
    /// Cette classe permet de récupérer des infos de base sur les tables et la base de donnée
    /// </summary>
    public class DatabaseInfoCollector
    {
        public ObservableCollection<TableInformation> TableInformations { get; set; }
        private readonly DbContext _dbContext;
        public List<string> AllProperties { get; set; }

        public DatabaseInfoCollector(DbContext _DbContext)
        {
            _dbContext = _DbContext ?? throw new ArgumentNullException(nameof(_DbContext));
            AllProperties = new List<string>();
            TableInformations = new ObservableCollection<TableInformation>();
            CollectInfosAsync().Wait();
        }


        public async Task CollectInfosAsync()
        {
            try
            {
                HashSet<string> properties = new HashSet<string>();
                TableInformations.Clear();
                var model = _dbContext.Model;

                foreach (var entityType in model.GetEntityTypes())
                {
                    foreach( var entity in entityType.GetProperties())
                    {
                        properties.Add(entity.Name);
                    }
                    // Utilisation de la méthode générique avec reflection
                    var countMethod = typeof(DatabaseInfoCollector)
                        .GetMethod(nameof(GetCountAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                        .MakeGenericMethod(entityType.ClrType);

                    var count = await (Task<int>)countMethod.Invoke(this, new object[] { });

                    var tableInfo = new TableInformation
                    {
                        Name = entityType.GetTableName() ?? entityType.Name,
                        Schema = entityType.GetSchema(),
                        PropertiesCount = entityType.GetProperties().Count(),
                        EntitiesCount = count
                    };

                    TableInformations.Add(tableInfo);
                }
            }
            catch (Exception ex)
            {
                throw new DatabaseCollectorException("Erreur lors de la collecte des informations de la base de données", ex);
            }
        }

        private async Task<int> GetCountAsync<T>() where T : class
        {
            return await _dbContext.Set<T>().CountAsync();
        }
    }

    public class DatabaseCollectorException : Exception
    {
        public DatabaseCollectorException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
