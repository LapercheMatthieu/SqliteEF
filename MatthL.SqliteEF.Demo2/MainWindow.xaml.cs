using MatthL.SqliteEF.Core.Managers;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MatthL.SqliteEF.Demo2
{
    public partial class MainWindow : Window
    {
        private List<MockSQLManager> _mockManagers;
        private DatabaseGeneralViewModel _generalViewModel;
        private DatabaseDetailViewModel _detailViewModel;

        public MainWindow()
        {
            InitializeComponent();
            InitializeDemo();
        }

        private void InitializeDemo()
        {
            // Créer 3 bases de données fictives
            _mockManagers = new List<MockSQLManager>
            {
                MockSQLManager.CreateConnectedDatabase("users", @"C:\Demo\Databases", ".db"),
                MockSQLManager.CreateConnectedDatabase("products", @"C:\Demo\Databases", ".sqlite"),
                MockSQLManager.CreateDisconnectedDatabase("orders", @"C:\Demo\Databases", ".db")
            };

            // Ajouter les vues compactes
            foreach (var manager in _mockManagers)
            {
                var compactViewModel = new DatabaseCompactViewModel(manager);
                var compactView = new DatabaseCompactView(compactViewModel);

                // Ajouter un espacement entre les vues
                compactView.Margin = new Thickness(0, 0, 0, 12);

                CompactViewsPanel.Children.Add(compactView);
            }

            // Afficher la vue générale avec la première base
            _generalViewModel = new DatabaseGeneralViewModel(_mockManagers[0]);
            var generalView = new DatabaseGeneralView(_generalViewModel);
            GeneralViewContainer.Content = generalView;

            // Afficher la vue détaillée avec la première base
            _detailViewModel = new DatabaseDetailViewModel(_mockManagers[0]);
            var detailView = new DatabaseDetailView(_detailViewModel);
            DetailViewContainer.Content = detailView;
        }

        protected override void OnClosed(EventArgs e)
        {
            // Disposer les ViewModels
            _generalViewModel?.Dispose();
            _detailViewModel?.Dispose();

            base.OnClosed(e);
        }
    }
}