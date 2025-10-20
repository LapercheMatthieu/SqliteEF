using CommunityToolkit.Mvvm.DependencyInjection;
using MatthL.SqliteEF.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Authorizations
{
    public interface IAuthorizationManager
    {
        IServiceAuthorization GetAuthorization<T>() where T : class, IBaseEntity;
    }
}
