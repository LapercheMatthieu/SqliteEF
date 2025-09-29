using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Authorizations
{
    public interface IAuthorizationManager
    {
        public bool CanCreate(string tableName);
        public bool CanRead(string tableName);
        public bool CanUpdate(string tableName);
        public bool CanDelete(string tableName);
    }
}
