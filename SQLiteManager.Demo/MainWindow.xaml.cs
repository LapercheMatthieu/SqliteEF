using Microsoft.EntityFrameworkCore;
using SQLiteManager.Authorizations;
using SQLiteManager.Managers;
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

namespace SQLiteManager.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            bigtest();

        }
        private async void bigtest()
        {
            var test = new SQLiteManagerTest();
            await test.RunAllTests();
        }
    }
}