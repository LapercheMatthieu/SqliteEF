using Microsoft.Extensions.DependencyInjection;
using NexusAIO.Common.Interfaces.Services;
using NexusAIO.Common.Interfaces.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NexusAIO.Core.Services.DialogManagers
{


    public class DialogManager : IDialogManager
    {
        //private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, object> _comboBoxMappings = new();

        public DialogManager()
        {
            //_serviceProvider = serviceProvider;
        }

        public void RegisterComboBoxDialog<TEntity>(object AttachedGrid) where TEntity : class
        {
            _comboBoxMappings[typeof(TEntity)] = AttachedGrid;
        }

        public async Task<object> GetComboBoxView<TEntity>() where TEntity : class
        {
            if (!_comboBoxMappings.TryGetValue(typeof(TEntity), out var viewType))
            {
                throw new InvalidOperationException($"No dialog registered for entity type {typeof(TEntity)}");
            }
            else
            {
                return viewType;
            }
            /*var view = (Window)ActivatorUtilities.CreateInstance(_serviceProvider, viewType);

            if (view.DataContext is IEntityAware<TEntity> viewModel)
            {
                viewModel.SetEntity(entity);
            }

            await Task.Run(() => Application.Current.Dispatcher.Invoke(() => view.ShowDialog()));*/
        }
    }

    // Interface pour les ViewModels qui ont besoin de l'entité

    // Registration des services
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDialogManager(this IServiceCollection services)
        {
            services.AddSingleton<IDialogManager, DialogManager>();
            return services;
        }
    }
}
