using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Authorizations.ServiceAuthorizations
{
    public class ReadOnlyAuthorization : IServiceAuthorization
    {
        public static readonly ReadOnlyAuthorization Instance = new();
        public bool CanCreate => false;
        public bool CanRead => true;
        public bool CanUpdate => false;
        public bool CanDelete => false;    
    }
}
