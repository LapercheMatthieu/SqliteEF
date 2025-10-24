
using MatthL.SqliteEF.Views.DatabaseCompactViews;
using MatthL.SqliteEF.Views.DatabaseDetailViews;
using MatthL.SqliteEF.Views.DatabaseGeneralViews;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text;
using System.Windows;

namespace MatthL.SqliteEF.Demo
{
    public partial class MainWindow : Window
    {
        private List<RealisticDatabaseSimulator> _simulators;
        private DatabaseGeneralViewModel _generalViewModel;
        private DatabaseDetailViewModel _detailViewModel;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) => await InitializeDemoAsync();
        }

        private async System.Threading.Tasks.Task InitializeDemoAsync()
        {
            try
            {
                StatusText.Text = "🔄 Initialisation des bases de données...";

                // Créer un dossier temporaire pour la démo
                var demoFolder = Path.Combine(Path.GetTempPath(), "SqliteEF_Demo");
                if (Directory.Exists(demoFolder))
                {
                    Directory.Delete(demoFolder, true);
                }
                Directory.CreateDirectory(demoFolder);

                _simulators = new List<RealisticDatabaseSimulator>();

                // Créer 3 bases de données avec activité simulée
                StatusText.Text = "📊 Création de users.db...";
                var usersSimulator = await RealisticDatabaseSimulator.CreateAsync("users", demoFolder, ".db");
                _simulators.Add(usersSimulator);

                StatusText.Text = "📊 Création de products.sqlite...";
                var productsSimulator = await RealisticDatabaseSimulator.CreateAsync("products", demoFolder, ".sqlite");
                _simulators.Add(productsSimulator);

                StatusText.Text = "📊 Création de orders.db...";
                var ordersSimulator = await RealisticDatabaseSimulator.CreateDisconnectedAsync("orders", demoFolder, ".db");
                _simulators.Add(ordersSimulator);

                // Ajouter les vues compactes
                foreach (var simulator in _simulators)
                {
                    var compactViewModel = new DatabaseCompactViewModel(simulator.Manager);
                    var compactView = new DatabaseCompactView(compactViewModel);
                    compactView.Margin = new Thickness(0, 0, 0, 12);

                    // Gérer le clic pour changer la vue détaillée
                    compactViewModel.DetailsRequested += (s, e) => SwitchToDatabase(simulator);

                    CompactViewsPanel.Children.Add(compactView);
                }

                // Afficher la vue générale avec la première base
                _generalViewModel = new DatabaseGeneralViewModel(_simulators[0].Manager);
                _generalViewModel.DetailsRequested += (s, e) => SwitchToDatabase(_simulators[0]);
                var generalView = new DatabaseGeneralView(_generalViewModel);
                GeneralViewContainer.Content = generalView;

                // Afficher la vue détaillée avec la première base
                _detailViewModel = new DatabaseDetailViewModel(_simulators[0].Manager);
                var detailView = new DatabaseDetailView(_detailViewModel);
                DetailViewContainer.Content = detailView;

                StatusText.Text = $"✅ Démo prête! {_simulators.Count} bases de données actives dans: {demoFolder}";

                // Afficher les informations de la démo
                ShowDemoInfo(demoFolder);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Erreur: {ex.Message}";
                MessageBox.Show($"Erreur lors de l'initialisation:\n{ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchToDatabase(RealisticDatabaseSimulator simulator)
        {
            // Disposer les anciens ViewModels
            _generalViewModel?.Dispose();
            _detailViewModel?.Dispose();

            // Créer les nouveaux ViewModels
            _generalViewModel = new DatabaseGeneralViewModel(simulator.Manager);
            _detailViewModel = new DatabaseDetailViewModel(simulator.Manager);

            // Mettre à jour les vues
            GeneralViewContainer.Content = new DatabaseGeneralView(_generalViewModel);
            DetailViewContainer.Content = new DatabaseDetailView(_detailViewModel);
        }

        private void ShowDemoInfo(string demoFolder)
        {
            var info = $@"
🎮 DÉMO INTERACTIVE SQLite Manager

📁 Dossier: {demoFolder}

🎯 Fonctionnalités actives:
   • 3 bases de données réelles avec Entity Framework Core
   • Opérations automatiques toutes les 2-8 secondes
   • Lectures, écritures, mises à jour, suppressions réelles
   • Indicateurs d'activité en temps réel
   • Health checks et statistiques réels

📖 Opérations simulées:
   • 40% Lectures simples (GetAll)
   • 30% Lectures filtrées (Where)
   • 15% Ajouts (Add)
   • 10% Mises à jour (Update)
   • 5% Suppressions (Delete)

🎨 Vues disponibles:
   • Vue Compacte (gauche, haut) - Cliquez pour changer de DB
   • Vue Générale (gauche, bas) - Actions rapides
   • Vue Détaillée (droite) - Monitoring complet

💡 Astuce: Regardez la console pour voir les opérations en direct!

🔄 Les bases de données se remplissent automatiquement.
   Observez les indicateurs d'activité s'animer en temps réel!
";

            MessageBox.Show(info, "Bienvenue dans la démo!", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void AddPeopleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_simulators != null && _simulators.Count > 0)
            {
                var result = await _simulators[0].AddMultiplePeopleAsync(5);
                if (result.IsSuccess)
                {
                    MessageBox.Show("✅ 5 personnes ajoutées avec succès!", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async void ComplexTransactionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_simulators != null && _simulators.Count > 0)
            {
                var result = await _simulators[0].PerformComplexTransactionAsync();
                if (result.IsSuccess)
                {
                    MessageBox.Show("✅ Transaction complexe réussie!", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_simulators != null && _simulators.Count > 0)
            {
                var result = await _simulators[0].CleanupModifiedPeopleAsync();
                if (result.IsSuccess)
                {
                    MessageBox.Show($"🧹 {result.Value} personnes nettoyées!", "Nettoyage", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async void StatsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_simulators != null && _simulators.Count > 0)
            {
                var result = await _simulators[0].GetDataStatisticsAsync();
                if (result.IsSuccess)
                {
                    MessageBox.Show($"📊 Statistiques:\n\n{result.Value}", "Statistiques des données", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Disposer les simulateurs et les ViewModels
            if (_simulators != null)
            {
                foreach (var simulator in _simulators)
                {
                    simulator?.Dispose();
                }
            }

            _generalViewModel?.Dispose();
            _detailViewModel?.Dispose();

            base.OnClosed(e);
        }
    }
}