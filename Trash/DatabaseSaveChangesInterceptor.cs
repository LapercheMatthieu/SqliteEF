using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Win32;
using NexusAIO.Core.DataBase.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusAIO.Core.DataBase.Interceptors
{
    public class DatabaseSaveChangesInterceptor : ISaveChangesInterceptor
    {
        public event Action<DatabaseWorkingStatus> WorkingStatusChanged;

        #region ******************************************** Sync methodes *****************************************
        public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            // Logique avant SaveChanges
            var entries = eventData.Context.ChangeTracker.Entries();
            foreach (var entry in entries)
            {
                Console.WriteLine($"Entity: {entry.Entity.GetType().Name}, State: {entry.State}");
            }
            return result;
        }

        public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            // Logique après SaveChanges
            return result;
        }

        public void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            // Logique en cas d'échec de SaveChanges
            Console.WriteLine($"SaveChanges failed: {eventData.Exception}");
        }
        #endregion

        #region ******************************************** ASync methodes *****************************************

        public async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            // Logique avant SaveChangesAsync
            WorkingStatusChanged?.Invoke(DatabaseWorkingStatus.Writing);

            /*var entries = eventData.Context.ChangeTracker.Entries();
            foreach (var entry in entries)
            {
                Console.WriteLine($"Entity: {entry.Entity.GetType().Name}, State: {entry.State}");
            }*/
            return result;
        }



        public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            WorkingStatusChanged?.Invoke(DatabaseWorkingStatus.Written);
            // Logique après SaveChangesAsync
            return result;
        }

        public async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            // Logique en cas d'échec de SaveChangesAsync
            Console.WriteLine($"SaveChangesAsync failed: {eventData.Exception}");
        }
        #endregion





    }
}
