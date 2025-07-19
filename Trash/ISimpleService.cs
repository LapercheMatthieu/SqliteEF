using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using NexusAIO.Common.Interfaces.Entities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NexusAIO.Common.Interfaces.Services
{
    public interface ISimpleService<T> where T : class, IBaseEntity
    {
        public ObservableCollection<T> Collection { get; set; }

        public event Action CollectionUpdated;

        public Task RefreshCollectionAsync();

        public Task<bool> AnyExist();

        public IQueryable<T> GetAllIncluding(params Expression<Func<T, object>>[] includeProperties);

        public string GetTableName();
    }
}
