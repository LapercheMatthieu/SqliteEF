using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Authorizations
{
    public interface IServiceAuthorization
    {
        bool CanCreate { get; }
        bool CanRead { get; }
        bool CanUpdate { get; }
        bool CanDelete { get; }
    }
}
