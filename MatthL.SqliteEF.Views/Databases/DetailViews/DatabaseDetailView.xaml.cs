using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MatthL.SqliteEF.Views.Databases.DetailViews
{
    /// <summary>
    /// Logique d'interaction pour DatabaseDetailView.xaml
    /// </summary>
    public partial class DatabaseDetailView : UserControl
    {
        public DatabaseDetailViewModel ViewModel { get; set; }
        public DatabaseDetailView(DatabaseDetailViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }
        public DatabaseDetailView()
        {
            InitializeComponent();
        }
        public void SetDataContext(DatabaseDetailViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
        }
    }
}
