using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace NexusAIO.Common.Interfaces.Services
{
    public interface IDialogManager
    {
        Task<object> GetComboBoxView<TEntity>() where TEntity : class;
        void RegisterComboBoxDialog<TEntity>(object AttachedGrid) where TEntity : class;
    }
}
