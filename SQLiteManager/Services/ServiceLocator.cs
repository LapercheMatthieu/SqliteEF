using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using SQLiteManager.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SQLiteManager.Services
{
    public static class ServiceLocator
    {
        private static readonly ConcurrentDictionary<Type, object> _services = new();
        private static IServiceProvider _provider;

        public static void Initialize(IServiceProvider serviceProvider)
        {
            _provider = serviceProvider;
        }

        public static BaseService<T> GetService<T>() where T : class, IBaseEntity
        {
            return (BaseService<T>)_services.GetOrAdd(
                typeof(T),
                _ => ActivatorUtilities.CreateInstance<BaseService<T>>(_provider)
            );
        }

    }
}
