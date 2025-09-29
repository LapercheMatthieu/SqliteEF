using MatthL.SqliteEF.Core.Models;
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
    public class RootSqliteEFRepository : ISqliteEFRepository
    {
        private List<ObservableCollection<IBaseEntity>> _subscribedCollections = new();
        /// <summary>
        /// Reflexion on all ObservableCollection to subscibe to events
        /// </summary>
        public void Initialize()
        {
            // Unsubscribe from previous collections if any
         /*   UnsubscribeAll();

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
                    var collection = property.GetValue(this) as INotifyCollectionChanged;

                    if (collection != null)
                    {
                        // Subscribe to collection changes
                        collection.CollectionChanged += OnCollectionChanged;
                        _subscribedCollections.Add(collection);

                        // Optional: Log or debug
                        System.Diagnostics.Debug.WriteLine(
                            $"Subscribed to ObservableCollection: {property.Name}");
                    }
                }
            }*/
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
                        foreach (IBaseEntity item in e.NewItems)
                        {
                            // Handle added items
                            OnItemAdded(item);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (IBaseEntity item in e.OldItems)
                        {
                            // Handle removed items
                            OnItemRemoved(item);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.OldItems != null)
                    {
                        foreach (IBaseEntity item in e.OldItems)
                        {
                            OnItemRemoved(item);
                        }
                    }
                    if (e.NewItems != null)
                    {
                        foreach (IBaseEntity item in e.NewItems)
                        {
                            OnItemAdded(item);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // Handle clear operation
                    OnCollectionReset();
                    break;
            }
        }

        /// <summary>
        /// Virtual method can be overriden for other use
        /// </summary>
        protected virtual void OnItemAdded(IBaseEntity item)
        {
            // Override in derived class to handle item addition
            // Example: Add to DbContext, etc.
        }

        /// <summary>
        /// Virtual method can be overriden for other use
        /// </summary>
        protected virtual void OnItemRemoved(IBaseEntity item)
        {
            // Override in derived class to handle item removal
            // Example: Remove from DbContext, etc.
        }

        /// <summary>
        /// Virtual method can be overriden for other use
        /// </summary>
        protected virtual void OnCollectionReset()
        {
            // Override in derived class to handle collection reset
            // Example: Clear related entities in DbContext
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
        }
    }
}
