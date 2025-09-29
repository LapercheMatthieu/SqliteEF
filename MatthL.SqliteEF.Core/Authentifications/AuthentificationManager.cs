using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Authentifications
{
    public class AuthentificationManager
    {
        public string UserName { get; set; } = "Not Identified";
        public AuthentificationManager(bool IdentificationIsMandatory)
        {
            if (IdentificationIsMandatory)
            {
                if (AuthentificationHelper.IsUserLoggedOn())
                {
                    UserName = AuthentificationHelper.GetUserName();
                }
            }
        }
    }
}
