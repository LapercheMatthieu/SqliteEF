using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusAIO.Features.DatabaseGroup.Models
{
    public partial class TableInformation : ObservableObject
    {
        [ObservableProperty]
        private string _Name;

        [ObservableProperty]
        private int _PropertiesCount;

        [ObservableProperty]
        private int _EntitiesCount;

        [ObservableProperty]
        private string _Schema;

        [ObservableProperty]
        private List<IEntityType> _Dependancies;

        public int DependancyCount
        {
            get
            {
                return Dependancies.Count;
            }
        }
    }
}
