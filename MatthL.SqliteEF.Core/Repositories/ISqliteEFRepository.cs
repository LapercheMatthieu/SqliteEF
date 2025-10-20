using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Repositories
{
    public interface ISqliteEFRepository
    {
        /// <summary>
        /// This function will catch all observable Collections events and push them to the SQLManager
        /// </summary>
        public void Initialize();

    }
}
