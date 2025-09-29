using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using MatthL.SqliteEF.Core.Models;
using System;
using System.Collections.Concurrent;

namespace MatthL.SqliteEF.Core.Services
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
