using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteManager.Authorizations
{
    public class AdminAuthorization : IAuthorizationManager
    {
        public bool CanCreate(string tableName) { return true; }
        public bool CanRead(string tableName) { return true; }
        public bool CanUpdate(string tableName) { return true; }
        public bool CanDelete(string tableName) { return true; }
    }
}
