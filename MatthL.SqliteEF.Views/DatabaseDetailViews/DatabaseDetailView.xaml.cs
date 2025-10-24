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

namespace MatthL.SqliteEF.Views.DatabaseDetailViews
{
    /// <summary>
    /// Logique d'interaction pour DatabaseDetailView.xaml
    /// </summary>
    public partial class DatabaseDetailView : UserControl
    {
        public DatabaseDetailViewModel ViewModel { get; set; }

        public DatabaseDetailView()
        {
            InitializeComponent();
            this.DataContextChanged += DatabaseDetailView_DataContextChanged;
        }

        private void DatabaseDetailView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(DataContext is  DatabaseDetailViewModel viewModel)
            {
                ViewModel = viewModel;
                DataContext = ViewModel;
            }
        }

        public DatabaseDetailView(DatabaseDetailViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        public void SetDataContext(DatabaseDetailViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
        }
    }
}
