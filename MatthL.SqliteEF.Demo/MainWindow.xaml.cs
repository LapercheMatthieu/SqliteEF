
using Microsoft.EntityFrameworkCore;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Models;
using System.IO;
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

namespace MatthL.SqliteEF.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            TestInitalizer initializer = new TestInitalizer();
            InitializeComponent();
            

            LogViewer viewer = new LogViewer();
            LogManager.Configure(Mechatro.WPF.ErrorLogger.Core.Enums.LogDestination.Memory);
            var wind = new Window();
            wind.Content = viewer;
            wind.Show();
            
            DatabaseDetails.SetDataContext(new DatabaseDetailViewModel(initializer._sqlManager));
            //bigtest();
        }
        private async void bigtest()
        {
            var test = new MatthL.SqliteEFTest();
            
            await test.RunAllTests();
        }

    }
}