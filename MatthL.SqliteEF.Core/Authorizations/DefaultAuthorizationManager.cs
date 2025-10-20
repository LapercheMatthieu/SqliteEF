using MatthL.SqliteEF.Core.Authorizations.ServiceAuthorizations;
using MatthL.SqliteEF.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Authorizations
{
    public class DefaultAuthorizationManager : IAuthorizationManager
    {
        private readonly Dictionary<Type, IServiceAuthorization> _authorizations = new();

        public IServiceAuthorization GetAuthorization<T>() where T : class, IBaseEntity
        {
            if (_authorizations.TryGetValue(typeof(T), out var auth))
                return auth;

            return AdminServiceAuthorization.Instance; // Défaut
        }
    }
}
