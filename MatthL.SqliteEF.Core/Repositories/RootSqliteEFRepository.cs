using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Repositories
{
    /// <summary>
    /// Base class for the repositories
    /// Allows automatic synchronization of the ObservableCollection of IBaseEntity
    /// Mandatory : The IBaseEntity must be in a dbset of the RootDBContext
    /// </summary>
    public class RootSqliteEFRepository //: ISqliteEFRepository
    {
        /*private List<ObservableCollection<IBaseEntity>> _subscribedCollections = new();
        private SQLManager<TContext> _SQLManager;
        public RootSqliteEFRepository(SQLManager SQLManager)
        {
            _SQLManager = SQLManager;
            Initialize();
            SQLManager.ConnectionStateChanged += SQLManager_ConnectionStateChanged;
        }


        private void SQLManager_ConnectionStateChanged(object? sender, Enums.ConnectionState e)
        {
            if(e == Enums.ConnectionState.Connected)
            {
                UnsubscribeAll();
                StartingRefreshStorage();
            }
        }
        private void StartingRefreshStorage()
        {
            // Get all properties of the current instance
            var properties = this.GetType().GetProperties(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Check if property is ObservableCollection<T> where T : IBaseEntity
                if (IsObservableCollectionOfBaseEntity(property.PropertyType))
                {
                    // Get the collection instance
                    var collection = property.GetValue(this) as ObservableCollection<IBaseEntity>;

                    if (collection != null)
                    {
                        // Subscribe to collection changes
                 //       _SQLManager.GetAllAsync<>();
                    }
                }
            }
        }
        /// <summary>
        /// Reflexion on all ObservableCollection to subscibe to events
        /// </summary>
        public void Initialize()
        {
            // Unsubscribe from previous collections if any
            UnsubscribeAll();

            // Get all properties of the current instance
            var properties = this.GetType().GetProperties(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance);

            foreach (var property in properties)
            {
                // Check if property is ObservableCollection<T> where T : IBaseEntity
                if (IsObservableCollectionOfBaseEntity(property.PropertyType))
                {
                    // Get the collection instance
                    var collection = property.GetValue(this) as ObservableCollection<IBaseEntity>;

                    if (collection != null)
                    {
                        // Subscribe to collection changes
                        collection.CollectionChanged += OnCollectionChanged;
                        _subscribedCollections.Add(collection);

                        // Optional: Log or debug
                        Result.Success($"Subscribed to ObservableCollection: {property.Name}",ResultLogger.Core.Enums.LogLevel.Info);
                    }
                }
            }
        }

        /// <summary>
        /// Check if a type is ObservableCollection<T> where T implements IBaseEntity
        /// </summary>
        private bool IsObservableCollectionOfBaseEntity(Type type)
        {
            // Check if it's a generic type
            if (!type.IsGenericType)
                return false;

            // Check if it's ObservableCollection<>
            var genericTypeDef = type.GetGenericTypeDefinition();
            if (genericTypeDef != typeof(ObservableCollection<>))
                return false;

            // Check if the generic argument implements IBaseEntity
            var genericArg = type.GetGenericArguments().FirstOrDefault();
            if (genericArg == null)
                return false;

            // Check if the type implements IBaseEntity (works for both class and interface)
            return typeof(IBaseEntity).IsAssignableFrom(genericArg);
        }

        /// <summary>
        /// Handle collection changes
        /// </summary>
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        OnItemsAdded(e.NewItems as IList<IBaseEntity>);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        OnItemsRemoved(e.OldItems as IList<IBaseEntity>);
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null)
                    {
                        OnItemsRemoved(e.OldItems as IList<IBaseEntity>);
                    }
                    if (e.NewItems != null)
                    {
                        OnItemsAdded(e.NewItems as IList<IBaseEntity>);
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // Handle clear operation
                    OnCollectionReset(sender);
                    break;
            }
        }

        /// <summary>
        /// Virtual method can be overriden for other use
        /// </summary>
        protected virtual async Task OnItemsAdded(IList<IBaseEntity> items)
        {
            // Override in derived class to handle item addition
            await _SQLManager.AddRangeAsync(items);
        }

        /// <summary>
        /// Virtual method can be overriden for other use
        /// </summary>
        protected virtual async Task OnItemsRemoved(IList<IBaseEntity> items)
        {
            // Override in derived class to handle item removal
            await _SQLManager.DeleteRangeAsync(items);
        }

        /// <summary>
        /// Virtual method can be overriden for other use
        /// </summary>
        protected virtual async Task OnCollectionReset(object? sender)
        {
            // Override in derived class to handle collection reset
            var selectedCollection = _subscribedCollections.Where(p => sender == p).FirstOrDefault();
            if(selectedCollection != null)
            {
                await _SQLManager.DeleteRangeAsync(selectedCollection);
            }
            
        }

        /// <summary>
        /// Unsubscribe from all collections
        /// </summary>
        private void UnsubscribeAll()
        {
            foreach (var collection in _subscribedCollections)
            {
                collection.CollectionChanged -= OnCollectionChanged;
            }
            _subscribedCollections.Clear();
        }

        /// <summary>
        /// Dispose pattern to ensure cleanup
        /// </summary>
        public void Dispose()
        {
            UnsubscribeAll();
        }*/
    }
}
