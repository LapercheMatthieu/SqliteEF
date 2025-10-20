using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Authorizations.ServiceAuthorizations
{
    public class WriteOnlyAuthorization : IServiceAuthorization
    {
        public static readonly WriteOnlyAuthorization Instance = new();
        public bool CanCreate => true;
        public bool CanRead => true;
        public bool CanUpdate => true;
        public bool CanDelete => false;
    }
}
