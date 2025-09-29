using System.Security.Principal;

namespace MatthL.SqliteEF.Core.Authentifications
{
    public static class AuthentificationHelper
    {

        public static bool IsUserLoggedOn()
        {
            WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();

            // Vérifie si l'utilisateur est authentifié et n'est pas un compte système
            if (windowsIdentity.IsAuthenticated &&
                !windowsIdentity.IsSystem &&
                !windowsIdentity.IsAnonymous)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static string GetUserName()
        {
            // Récupère le nom d'utilisateur à partir du nom de compte Windows
            string windowsAccountName = Environment.UserName;
            return windowsAccountName;
        }
        /// <summary>
        /// Renvoi un string plus lisible
        /// </summary>
        /// <returns></returns>
        public static string GetCleanUserName()
        {
            // Récupère le nom d'utilisateur à partir du nom de compte Windows
            string windowsAccountName = Environment.UserName;
            string ReturnString = windowsAccountName.Replace('.', ' ');
            ReturnString = ReturnString.Replace('_', ' ');
            return ReturnString;
        }
    }
}