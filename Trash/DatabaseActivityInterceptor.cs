using Microsoft.EntityFrameworkCore.Diagnostics;
using NexusAIO.Core.DataBase.Classes;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusAIO.Core.DataBase.Interceptors
{
    public class DatabaseActivityInterceptor : DbCommandInterceptor
    {
        public event Action<DatabaseWorkingStatus> WorkingStatusChanged;

        // Méthodes synchrones
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            base.ReaderExecuting(command, eventData, result);
            // Logique avant l'exécution d'une requête SELECT
            Console.WriteLine($"Executing SELECT: {command.CommandText}");
            return result;
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            // Logique avant l'exécution des commandes INSERT, UPDATE, DELETE
            Console.WriteLine($"Executing NonQuery: {command.CommandText}");
            base.NonQueryExecuting(command, eventData, result);
            return result;
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            // Logique avant l'exécution des requêtes scalar
            Console.WriteLine($"Executing Scalar: {command.CommandText}");
            return result;
        }

        // Méthodes asynchrones
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            // Logique avant l'exécution d'une requête SELECT async
            WorkingStatusChanged?.Invoke(DatabaseWorkingStatus.Reading);
            base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            Console.WriteLine($"Executing SELECT async: {command.CommandText}");
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            WorkingStatusChanged?.Invoke(DatabaseWorkingStatus.Read);
            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            // Logique avant l'exécution des commandes INSERT, UPDATE, DELETE async
            Console.WriteLine($"Executing NonQuery async: {command.CommandText}");
            return result;
        }

        public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            // Logique avant l'exécution des requêtes scalar async
            Console.WriteLine($"Executing Scalar async: {command.CommandText}");
            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
