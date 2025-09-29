using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Models;

namespace NexusAIO.Features.DatabaseGroup.Models
{
    /*
    public partial class DatabaseModel : ObservableObject
    {
        #region Variables!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        [ObservableProperty]
        private string _referenceProperty; //Ceci est la référence a la propriété stockée dans les settings application

        [ObservableProperty]
        private string _DatabasePath;

        [ObservableProperty]
        private bool _DatabaseConnectionIsOK;

        [ObservableProperty]
        private bool _DatabaseIsOK;

        [ObservableProperty]
        private bool _DatabasePathIsOK;

        [ObservableProperty]
        private bool _DatabaseStatusIsOK;

        [ObservableProperty]
        private string _errorMessage;

        [ObservableProperty]
        private int _NBSeeds = 20;

        public readonly CommonDBContext _dbContext;

        public event Action SeedingRequested;


        #endregion Variables!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        #region **************************************** WORKING STATUS ********************************************
        public event Action<DatabaseWorkingStatus> WorkingStatusChanged;

        [ObservableProperty]
        private DatabaseWorkingStatus _DBWorkingStatus;


        partial void OnDatabaseIsOKChanged(bool value)
        {
            if (value)
            {
                DBWorkingStatus = DatabaseWorkingStatus.Still;

            }
        }
        partial void OnDatabaseConnectionIsOKChanged(bool value)
        {
            if (!value)
            {
                DBWorkingStatus = DatabaseWorkingStatus.ConnectionProblem;

            }
        }
        partial void OnDatabasePathIsOKChanged(bool value)
        {
            if (!value)
            {
                DBWorkingStatus = DatabaseWorkingStatus.NotExisting;

            }
        }
        partial void OnDatabaseStatusIsOKChanged(bool value)
        {
            if (!value)
            {
                DBWorkingStatus = DatabaseWorkingStatus.UpdateProblem;

            }
        }
        partial void OnDBWorkingStatusChanged(DatabaseWorkingStatus oldValue, DatabaseWorkingStatus newValue)
        {
            if(oldValue != newValue)
            {
                WorkingStatusChanged?.Invoke(newValue);
            }
        }
        private void DbContext_DatabaseWorkingStatusChanged(Core.DataBase.Classes.DatabaseWorkingStatus obj)
        {
            DBWorkingStatus = obj;
        }
        #endregion
        #region InitialIsations!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        public DatabaseModel(CommonDBContext dbContext)
        {
            DBWorkingStatus = DatabaseWorkingStatus.NotExisting;
            InitializationOfRelayCommands();
            InitializationOfVariables();
            _dbContext = dbContext;
            dbContext.DatabaseWorkingStatusChanged += DbContext_DatabaseWorkingStatusChanged;

        }
        private void InitializationOfRelayCommands()
        {
            DatabasePathSelection = new RelayCommand(ExecuteDatabasePathSelection);
            DatabaseConnectionTest = new RelayCommand(ExecuteDatabaseConnectionTest);
            DatabaseStatusCheck = new RelayCommand(ExecuteDatabaseStatusCheck);
            DatabaseDestruction = new RelayCommand(ExecuteDatabaseDestruction);
            DatabaseCreation = new RelayCommand(ExecuteDatabaseCreation);
            DatabaseCopy = new RelayCommand(ExecuteDatabaseCopy);
        }

        private void InitializationOfVariables()
        {
            DatabaseIsOK = false;
            DatabasePathIsOK = false;
            DatabaseStatusIsOK = false;
            DatabaseConnectionIsOK = false;
        }

        #endregion InitialIsations!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        #region Commands!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        public ICommand DatabaseConnectionTest { get; set; }
        public ICommand DatabaseCopy { get; set; }
        public ICommand DatabaseCreation { get; set; }
        public ICommand DatabaseDestruction { get; set; }
        public ICommand DatabasePathSelection { get; set; }
        public ICommand DatabaseStatusCheck { get; set; }

        #endregion Commands!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        #region Execution!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        private void ExecuteDatabaseConnectionTest()
        {
            CheckDatabaseConnection();
        }

        private void ExecuteDatabaseCopy()
        {
            string NouvelleDatabase = OpenSaveFileSelector();

            CopyDatabase(NouvelleDatabase);
        }

        private void ExecuteDatabaseCreation()
        {
            string selectedPath = OpenSaveFileSelector();
            if (!string.IsNullOrEmpty(selectedPath))
            {
                ModifyDatabasePath(selectedPath);
                CreateDatabase();
                FullCheckDatabase();
                if (ReferenceProperty == "LocalDBPath")
                {
                    SeedingRequested?.Invoke();
                }
            }
        }

        public virtual void ExecuteDatabaseDestruction()
        {
        }

        private void ExecuteDatabasePathSelection()
        {
            string selectedPath = OpenFileSelector();
            if (!string.IsNullOrEmpty(selectedPath))
            {
                ModifyDatabasePath(selectedPath);
                CheckDatabasePath();
            }
        }

        private void ExecuteDatabaseStatusCheck()
        {
            CheckDatabaseStatus();
        }

        [RelayCommand]
        public async Task DatabaseSeeding()
        {
            DBWorkingStatus = DatabaseWorkingStatus.Writing; // Ajoutez cet état si nécessaire
            try
            {
                await Task.Run(async () =>
                {
                    RecreateDatabase();
                    var monSeeder = new AutoBogusSeeder(_dbContext);
                    await monSeeder.SeedDatabaseAsync(_NBSeeds);
                });
            }
            finally
            {
                FullCheckDatabase();
            }
        }

        [RelayCommand]
        public void DatabaseChecking()
        {
            DatabaseCoordinator myCoordinator = new DatabaseCoordinator(_dbContext);
            var test = myCoordinator.DependancyForgotten;
        }

        #endregion Execution!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        #region DatabaseVerification!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        public void CheckDatabasePath()
        {
            try
            {
                if (File.Exists(DatabasePath) && DatabasePath.Contains(".db"))
                {
                    DatabasePathIsOK = true;
                }
                else
                {
                    DatabasePathIsOK = false;
                }
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }

        public void CheckDatabaseConnection()
        {
            try
            {
                if (DatabasePathIsOK)
                {
                    DatabaseConnectionIsOK = _dbContext.Database.CanConnect();
                }
                else
                {
                    DatabaseConnectionIsOK = false;
                }
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }

        public void CheckDatabaseStatus()
        {
            try
            {
                if (DatabaseConnectionIsOK)
                {
                    if (_dbContext.Database.GetPendingMigrations().Any())
                    {
                        DatabaseStatusIsOK = false;
                    }
                    else
                    {
                        DatabaseStatusIsOK = true;
                    }
                }
                else
                {
                    DatabaseStatusIsOK = false;
                }
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
                DatabaseStatusIsOK = false;
            }
        }

        public void FullCheckDatabase()
        {
            CheckDatabasePath();
            CheckDatabaseConnection();
            CheckDatabaseStatus();
            DatabaseIsOK = (DatabasePathIsOK && DatabaseConnectionIsOK && DatabaseStatusIsOK);
        }

        #endregion DatabaseVerification!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        #region DatabaseOperations!!!!!!!

        public void CopyDatabase(string CopiedDatabasePath)
        {
            try
            {
                File.Copy(DatabasePath, CopiedDatabasePath);
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }

        public void CreateDatabase()
        {
            try
            {
                _dbContext.Database.EnsureCreated();
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }

        public void DestructDatabase()
        {
            try
            {
                _dbContext.Database.EnsureDeleted();
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }

        public void RecreateDatabase()
        {
            DestructDatabase();
            CreateDatabase();
            _dbContext.ChangeTracker.Clear();
        }

        public void ModifyDatabasePath(string MyPath)
        {
            DatabasePath = MyPath;
            InfrastructureSettings.Default[ReferenceProperty] = MyPath;
            InfrastructureSettings.Default.Save();
        }

        #endregion DatabaseOperations!!!!!!!

        #region Autre

        private string OpenFileSelector()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Database|*.db|All Files|*.*"; // Filter for specific file types
            openFileDialog.Title = "Open File";
            string selectedFilePath = "";
            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                // Process the selected file path as needed
            }
            return selectedFilePath;
        }

        private string OpenSaveFileSelector()
        {
            SaveFileDialog SaveFileDialog = new SaveFileDialog();
            SaveFileDialog.Filter = "Text Files|*.db|All Files|*.*"; // Filter for specific file types
            SaveFileDialog.Title = "Save File";
            SaveFileDialog.AddExtension = true;
            string selectedFilePath = "";
            if (SaveFileDialog.ShowDialog() == true)
            {
                selectedFilePath = SaveFileDialog.FileName;
                // Process the selected file path as needed
            }
            return selectedFilePath;
        }

        partial void OnErrorMessageChanged(string value)
        {
            MessageBox.Show(value, "Erreur Base De Donnée");
        }

        #endregion Autre
    }*/
}