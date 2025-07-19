using CommunityToolkit.Mvvm.ComponentModel;
using NexusAIO.Common.Interfaces.Entities;
using NexusAIO.Common.Interfaces.Services;
using NexusAIO.Common.Interfaces.ViewModels;
using NexusAIO.Core.Services;
using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;

namespace NexusAIO.Core.ViewModels.Standard
{
    public partial class BaseVM<T> : ObservableObject, IBaseVM where T : class, IBaseEntity
    {

        #region ************************************** PROPRIETES **************************************
        protected readonly IService<T> _service;

        public ObservableCollection<T> Collection
        {
            get
            {
                return _service.Collection;
            }
        }
        [ObservableProperty]
        private CollectionViewSource _viewSource;
        #endregion
        #region ************************************** CONSTRUCTEUR **************************************
        public BaseVM()
        {
            _service = ServiceLocator.GetService<T>();
            _service.CollectionUpdated += OnServiceCollectionUpdated;
            ViewSource = new CollectionViewSource();
            UpdateViewSource();
        }
        #endregion
        #region ************************************** PUBLIC FONCTIONS **************************************
        public bool CollectionAny()
        {
            return _service.Collection.Any();
        }

        public IEnumerable GetCollection()
        {
            return Collection;
        }
        public virtual void Dispose()
        {
            _service.CollectionUpdated -= OnServiceCollectionUpdated;
        }

        public void Refresh()
        {
            _service.RefreshCollectionAsync();
        }
        #endregion
        #region ************************************** PRIVATE FUNCTIONS **************************************
        private void OnServiceCollectionUpdated()
        {
            
            OnPropertyChanged(nameof(Collection));
        }

        private T Converter(IBaseEntity entity)
        {
            try
            {
                T myEntity = (T)entity;
                return myEntity;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private void UpdateViewSource()
        {
            ViewSource.Source = null;
            ViewSource.Source = Collection;
        }
        #endregion

        #region *********************************** CRUD ***********************************
        protected async Task AddItemAsync(T item)
        {
            await _service.AddAsync(item);
        }

        protected async Task UpdateItemAsync(T item)
        {
            await _service.UpdateAsync(item);
        }

        protected async Task DeleteItem(T item)
        {
            await _service.DeleteAsync(item);
        }
        public async Task DeleteItem(int id)
        {
            T myitem = Collection.Where(p => p.Id == id).FirstOrDefault();
            if(myitem != null)
            {
                await _service.DeleteAsync(myitem);
            }
        }
        
        public async Task SaveModifications(IBaseEntity MyEntity)
        {
            try
            {
                T entity = (T)MyEntity;
                if (entity.Id == 0)
                {
                    await _service.AddAsync(entity);
                }
                else
                {
                    await _service.UpdateAsync(entity);
                }


            }
            catch (Exception ex)
            {
                // Vérifier si une exception interne a été levée
                if (ex.InnerException != null)
                {
                    // Afficher le message d'erreur de l'exception interne
                    MessageBox.Show(ex.InnerException.Message);
                }
                else
                {
                    // Afficher le message d'erreur de l'exception externe
                    MessageBox.Show(ex.Message);
                }
            }
        }
        
        #endregion
       
    }

}
