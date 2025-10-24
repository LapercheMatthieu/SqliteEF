using MatthL.SqliteEF.Views.DatabaseDetailViews;
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

namespace MatthL.SqliteEF.Views.DatabaseCompactViews
{
    /// <summary>
    /// Logique d'interaction pour DatabaseCompactView.xaml
    /// </summary>
    public partial class DatabaseCompactView : UserControl
    {
        public DatabaseCompactViewModel ViewModel { get; set; }

        public DatabaseCompactView()
        {
            InitializeComponent();
            this.DataContextChanged += DatabaseCompactView_DataContextChanged;
        }

        private void DatabaseCompactView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DatabaseCompactViewModel viewModel)
            {
                ViewModel = viewModel;
                DataContext = ViewModel;
            }
        }

        public DatabaseCompactView(DatabaseCompactViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = ViewModel;
        }

        public void SetDataContext(DatabaseCompactViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
        }
    }
}
