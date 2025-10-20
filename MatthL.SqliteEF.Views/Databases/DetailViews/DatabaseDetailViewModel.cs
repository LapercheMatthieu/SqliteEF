using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Models;
using MatthL.SqliteEF.Views.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;

namespace MatthL.SqliteEF.Views.Databases.DetailViews
{
    public partial class DatabaseDetailViewModel : ObservableObject
    {
        [ObservableProperty] private bool _IsOk;
        [ObservableProperty] private string _Name;
        [ObservableProperty] private string _Folder;
        [ObservableProperty] private string _DatabaseCreationDate ;
        [ObservableProperty] private string _DatabaseUpdateDate;
        [ObservableProperty] private string _SizeString;

        [ObservableProperty] private string _GenericDatabaseName = "Base de donnée";
        public SQLManager Manager { get; set; }

        public DatabaseDetailViewModel(SQLManager manager)
        {
            Manager = manager;
            Manager.ConnectionStateChanged += Manager_ConnectionStateChanged;
        }

        private void Manager_ConnectionStateChanged(object? sender, Core.Enums.ConnectionState e)
        {
            RefreshView(e == Core.Enums.ConnectionState.Connected);
        }
        private async void RefreshView(bool IsConnected)
        {
            if (IsConnected)
            {
                IsOk = true;
                Name = Manager.GetFileName;
                Folder = Manager.GetFolderPath;
                DatabaseCreationDate =GetCreationDate();
                DatabaseUpdateDate = GetUpdateDate();
                SizeString = FileSizeHelper.GetFormattedSize(Manager.GetFileSize);
            }
            else
            {
                IsOk = false;
            }
        }
        private string GetCreationDate()
        {
            if (IsOk)
            {
                var time = new FileInfo(Manager.GetFullPath).CreationTimeUtc;

                return time.ToString("dd-MMMM at HH:mm");
            }
            else
            {
                return "";
            }
        }
        private string GetUpdateDate()
        {
            if (IsOk)
            {
                var time = new FileInfo(Manager.GetFullPath).LastWriteTimeUtc;

                return time.ToString("dd-MMMM at HH:mm");
            }
            else
            {
                return "";
            }
        }

        [RelayCommand]
        public async Task SelectDatabase(string test = "")
        {
            if(test == "" || test == null)
            {
                var choice = Application.Current.Dispatcher.Invoke(bool () =>
                {
                    OpenFileDialog dialog = new OpenFileDialog();
                    if (dialog.ShowDialog() == true)
                    {
                        test = dialog.FileName;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });
                if (choice == false) return;
            }
            Manager.SetPaths(Directory.GetParent(test).FullName, System.IO.Path.GetFileNameWithoutExtension(test));
            var result = await Manager.ConnectAsync();
        }

        [RelayCommand]
        public async Task CreateDatabase(string test = "")
        {

            if (test == "" || test == null)
            {
                var choice = Application.Current.Dispatcher.Invoke(bool() =>
                {
                    SaveFileDialog dialog = new SaveFileDialog();
                    if (dialog.ShowDialog() == true)
                    {
                        test = dialog.FileName;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                });
                if (choice == false) return;
            }
            
            Manager.SetPaths(Directory.GetParent(test).FullName, System.IO.Path.GetFileNameWithoutExtension(test));
            var result = await Manager.Create();
        }

        [RelayCommand]
        public async Task DeleteDatabase()
        {
            await Manager.DeleteCurrentDatabase();
        }

        [RelayCommand]
        public void OpenFolder()
        {

        }

        [RelayCommand]
        public void ShowInformations()
        {

        }
    }
}
