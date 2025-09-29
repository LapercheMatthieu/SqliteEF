using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace MatthL.SqliteEF.Authorizations
{
    /// <summary>
    /// Cette classe permet d'attribuer des authorizations. Elle utilise soit le role attribué, soit le role Admin soit le role Invité
    /// Le role invité est attribué si Aucun role n'est trouvé
    /// Le role Admin est attribué dans quel cas ??? 
    /// </summary>
    public static class AuthorizationHelper
    {
        private static ObservableCollection<UserRight> MyUserRights;
        public static bool IsAdmin;
        public static bool IsInvited;

        public static void SetupAuthorizationHelper()
        {
            IService<UserRight> MyService = ServiceLocator.GetService<UserRight>();
            IsAdmin = false;
            MyUserRights = new ObservableCollection<UserRight>();
            MyUserRights = MyService.Collection;
        }



        public static bool CanCreate(string tableName)
        {
            if (IsAdmin)
            {
                return true;
            }
            else
            {
                UserRight MyRights = MyUserRights.FirstOrDefault(r => r.TableName == tableName);
                return MyRights.CanCreate;
            }
        }

        public static bool CanRead(string tableName)
        {
            if (IsAdmin)
            {
                return true;
            }
            else
            {
                UserRight MyRights = MyUserRights.FirstOrDefault(r => r.TableName == tableName);
                return MyRights.CanRead;
            }
        }

        public static bool CanUpdate(string tableName)
        {
            if (IsAdmin)
            {
                return true;
            }
            else
            {
                UserRight MyRights = MyUserRights.FirstOrDefault(r => r.TableName == tableName);
                return MyRights.CanUpdate;
            }
        }

        public static bool CanDelete(string tableName)
        {
            if (IsAdmin)
            {
                return true;
            }
            else
            {
                UserRight MyRights = MyUserRights.FirstOrDefault(r => r.TableName == tableName);
                return MyRights.CanDelete;
            }
        }

        public static List<string> GetListOfTables()
        {
            var entityTypes = Ioc.Default.GetService<LocalDBContext>().Model.GetEntityTypes();
            List<string> MyReturnList = new List<string>();

            foreach (var entityType in entityTypes)
            {
                MyReturnList.Add(entityType.GetTableName());
            }
            return MyReturnList;
        }
    }
}