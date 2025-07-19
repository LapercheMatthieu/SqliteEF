using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteManager.Services
{
    public static class QueryableExtensions
    {
        /// <summary>
        /// Inclut automatiquement toutes les propriétés de navigation de l'entité
        /// </summary>
        /// <typeparam name="TEntity">Type de l'entité</typeparam>
        /// <param name="query">Requête à étendre</param>
        /// <param name="context">Contexte de base de données</param>
        /// <returns>Requête avec toutes les navigations incluses</returns>
        public static IQueryable<TEntity> IncludeAllNavigations<TEntity>(
            this IQueryable<TEntity> query,
            DbContext context) where TEntity : class
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity));

            if (entityType == null)
                return query;

            // Include standard navigations
            foreach (var navigation in entityType.GetNavigations())
            {
                query = query.Include(navigation.Name);
            }

            // Include skip navigations (many-to-many relationships)
            foreach (var skipNavigation in entityType.GetSkipNavigations())
            {
                query = query.Include(skipNavigation.Name);
            }

            return query;
        }
    }
}
