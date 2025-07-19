using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteManager.Authentifications
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
