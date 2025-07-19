using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NexusAIO.Core.Database.Contexts;
using NexusAIO.Core.Database.Tools;
using System.Reflection;

namespace NexusAIO.Services
{
    public class GlobalSeedDatas
    {
        /*public GlobalSeedDatas()
        {
            var myLocalDB = Ioc.Default.GetService<LocalDatabaseViewModel>();
            myLocalDB.SeedingRequested += MyLocalDB_SeedingRequested;
        }

        private void MyLocalDB_SeedingRequested()
        {
            using (var context = new LocalDBContext())
            {
                var seeder = new DatabaseSeeder(context);
                seeder.SeedDatabase();
            }
        }
        /*Ancienne méthode
        private void MyLocalDB_SeedingRequested()
        {


            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.FullName.StartsWith("NexusAIO.Features"));

            foreach (var assembly in assemblies)
            {
                RequestDataSeeds(assembly);
                RequestEnumSeeds(assembly);
            }
        }*/

       /* public void RequestDataSeeds(Assembly assembly)
        {
            var PopulateTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Name.StartsWith("Populate"));

            foreach (Type populatetype in PopulateTypes)
            {
                Ioc.Default.GetService(populatetype);
            }
        }

        public void RequestEnumSeeds(Assembly assembly)
        {
            var PopulateTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Name.StartsWith("EnumSeeds"));

            foreach (Type populatetype in PopulateTypes)
            {
                Ioc.Default.GetService(populatetype);
            }
        }*/
    }
}