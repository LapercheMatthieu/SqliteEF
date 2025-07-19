using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusAIO.Core.DataBase.Interceptors
{
    public class DatabaseInterceptor : IDbInterceptor
    {
        public void Executing(DbCommand command)
        {
            // Début de l'exécution
        }

        public void Executed(DbCommand command)
        {
            // Fin de l'exécution
        }
    }
}
