using Microsoft.EntityFrameworkCore;
using MatthL.SqliteEF.Authorizations;
using MatthL.SqliteEF.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Demo
{
    public class TestInitalizer
    {
        public string _testFolder { get; set; }
        public string _testDbName { get; set; }
        public SQLManager _sqlManager { get; set; }
        public TestDbContext _dbContext { get; set; }

        public TestInitalizer()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "SQLiteManagerTest");
            if (Directory.Exists(_testFolder))
            {
                var directory = new DirectoryInfo(_testFolder);
                directory.Delete(true);
            }
            _testDbName = "test_database.db";
            Directory.CreateDirectory(_testFolder);

            Debug.WriteLine($"Test folder: {_testFolder}");
            Debug.WriteLine($"Test database: {_testDbName}");

            Task.Run(()=> InitializeDB());
        }

        private async Task InitializeDB()
        {
            _dbContext = new TestDbContext();
            _sqlManager = new SQLManager(() => new TestDbContext(), _testFolder, _testDbName, new AdminAuthorization());
            //_sqlManager = new SQLManager(_dbContext);
            

        }
    }
}
