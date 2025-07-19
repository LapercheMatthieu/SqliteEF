using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using NexusAIO.Common.Interfaces.Entities;
using NexusAIO.Common.Interfaces.Services;
using NexusAIO.Common.Interfaces.ViewModels;
using NexusAIO.Core.Entities.Primitive;
using NexusAIO.Core.Services;
using System.Reflection;
using System.Windows;

namespace NexusAIO.Core.ViewModels.Standard
{
    public partial class BaseVMSelection<T> : BaseVM<T>, IBaseVMSelection where T : class, IBaseEntity, new()
    {
        [ObservableProperty]
        private T _selectedEntity;

        [ObservableProperty]
        private T _detailsEntity;

        [ObservableProperty]
        private bool _detailsEntityIsBlocked;

        [ObservableProperty]
        private bool _isSavable;

        public BaseVMSelection()
        {
            _detailsEntityIsBlocked = false;
        }

        partial void OnSelectedEntityChanged(T value)
        {
            RefreshDetailsEntity();
        }

        public object GetDetailsEntity()
        {
            return DetailsEntity;
        }

        public void RefreshDetailsEntity()
        {
            /* PARTIE JSON cause des problèmes avec EntityFramework
            if (!DetailsEntityIsBlocked)
            {
                DetailsEntity = null;
                var settings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                // Sérialise l'objet en une chaîne JSON
                string json = JsonConvert.SerializeObject(SelectedEntity, settings);

                // Désérialise la chaîne JSON en une nouvelle instance de l'objet
                DetailsEntity = JsonConvert.DeserializeObject<T>(json);
            }
            */
            if (!DetailsEntityIsBlocked && SelectedEntity != null)
            {
                DetailsEntity = SelectedEntity;
            }

        }

        public virtual void SetSelectedEntity(int IndexOfTheEntity)
        {
            SelectedEntity = Collection[IndexOfTheEntity];
        }

        public void CreateNewEntity()
        {
            DetailsEntity = new T();
        }

        public virtual void DeleteEntity()
        {
            _service.DeleteAsync(DetailsEntity);
        }

        public virtual void CheckSavability()
        {
            IsSavable = _service.IsSavable(DetailsEntity);
        }

        public virtual void SaveEntity()
        {
            try
            {
                if (DetailsEntity.Id == 0)
                {
                    // If the product has no ID (it's a new product), add it to the context
                    _service.AddAsync(DetailsEntity);
                }
                else
                {
                    // If the product has an ID (it's an existing product), mark it as modified
                    _service.UpdateAsync(DetailsEntity);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void ModifyDetailsEntity(object MyEntity)
        {
            if (MyEntity is T)
            {
                DetailsEntity = (T)MyEntity;
            }
        }

        public virtual void AutomaticFilling()
        {
        }

        public object GetSpecificPropertyValue(string propertyName)
        {
            if (DetailsEntity == null) { return null; }
            object propertyValue = DetailsEntity.GetType().GetProperty(propertyName)?.GetValue(DetailsEntity);
            return propertyValue;
        }

        public void AddObject(object obj)
        {
            PropertyInfo[] properties = obj.GetType().GetProperties();

            // Parcourir les propriétés
            foreach (PropertyInfo property in properties)
            {
                // Vérifier si la propriété est de type ICollection<T>
                if (typeof(ICollection<>).IsAssignableFrom(property.PropertyType))
                {
                    // Récupérer le type d'élément de la collection
                    Type elementType = property.PropertyType.GetGenericArguments()[0];

                    // Vérifier si l'objet est du même type que les éléments de la collection
                    if (elementType.IsAssignableFrom(obj.GetType()))
                    {
                        // Ajouter l'objet à la collection
                        ((ICollection<object>)property.GetValue(this)).Add(obj);
                    }
                }
            }
        }
    }
}