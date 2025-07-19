using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteManager.Models
{
    public class RootDbContext : DbContext
    {
        public string DatabasePath;

        public RootDbContext()
        {
        }
        /// <summary>
        /// Loop a travers tous les DBSets pour appliquer la configuration
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Récupérer tous les DbSets du contexte
            var dbSetProperties = GetType().GetProperties()
                .Where(p => p.PropertyType.IsGenericType &&
                           p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

            foreach (var dbSetProperty in dbSetProperties)
            {
                // Récupérer le type d'entité de ce DbSet
                var entityType = dbSetProperty.PropertyType.GetGenericArguments()[0];

                // Vérifier si l'entité implémente ISelfConfiguringEntity
                if (typeof(IBaseEntity).IsAssignableFrom(entityType))
                {
                    // Créer une instance (nécessaire seulement pour la configuration)
                    var entityInstance = Activator.CreateInstance(entityType) as IBaseEntity;
                    entityInstance?.ConfigureEntity(modelBuilder);
                }
            }

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DatabasePath}")
                .EnableSensitiveDataLogging()
                .LogTo(message => Debug.WriteLine(message),
                  LogLevel.Information);
        }
    }
}
