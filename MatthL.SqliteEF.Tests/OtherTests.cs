using FluentAssertions;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SQLiteManager.Tests
{
    public class OtherTests
    {

        [Fact]
        public async Task DifferentExtension_ShouldBuildWithCorrectExtension()
        {
            // Act
            string _testDbPath = Path.Combine(Path.GetTempPath(), "SQLiteTests");
            string _testDbName = $"TestDb_{Guid.NewGuid()}";
            string Extension = ".macq";
            Directory.CreateDirectory(_testDbPath);

            var _sqlManager = new SQLManager(
                path => new TestDbContext(path),
                _testDbPath,
                _testDbName,
                Extension
            );
            var creationResult = await _sqlManager.Create();
            var connectionResult = await _sqlManager.ConnectAsync();


            // Assert
            File.Exists(_sqlManager.GetFullPath).Should().BeTrue();
            var fileextension = new FileInfo(_sqlManager.GetFullPath);
            fileextension.Extension.Should().BeEquivalentTo( Extension);

            await _sqlManager.DisconnectAsync();
            await _sqlManager.DeleteCurrentDatabase();

            if (Directory.Exists(_testDbPath))
            {
                try
                {
                    Directory.Delete(_testDbPath, true);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

    }
}
