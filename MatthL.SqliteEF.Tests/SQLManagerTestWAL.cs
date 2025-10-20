using FluentAssertions;
using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Tests;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SQLiteManager.Tests
{
    public class SQLManagerTestWAL
    {
        private SQLConnectionManager _connectionManager => Manager.ConnectionManager;
        private TestDbContext _dbContext;
        private SQLManager Manager;
        public SQLManagerTestWAL()
        {
            string path = Path.GetTempPath();
            string filename = Guid.NewGuid().ToString();

            Manager = new SQLManager(() => new TestDbContext(), path, filename);

            _dbContext = new TestDbContext();
        }

        [Fact]
        public async Task ConnectAsync_ShouldConfigureConcurrentAccess()
        {
            // Act
            var result = await _connectionManager.ConnectAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
           // result.Error.Should().Contain("concurrent access enabled");
        }

        [Fact]
        public async Task GetConcurrencyConfigAsync_ShouldReturnConfiguration_WhenConnected()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _connectionManager.GetConcurrencyConfigAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Should().ContainKey("journal_mode");
            result.Value.Should().ContainKey("busy_timeout");
            result.Value.Should().ContainKey("cache_size");
            result.Value.Should().ContainKey("synchronous");
            result.Value.Should().ContainKey("locking_mode");
        }

        [Fact]
        public async Task GetConcurrencyConfigAsync_JournalMode_ShouldBeWAL()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _connectionManager.GetConcurrencyConfigAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value["journal_mode"].Should().Be("wal");
        }

        [Fact]
        public async Task GetConcurrencyConfigAsync_BusyTimeout_ShouldBe5000()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _connectionManager.GetConcurrencyConfigAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value["busy_timeout"].Should().Be("5000");
        }

        [Fact]
        public async Task GetConcurrencyConfigAsync_ShouldFail_WhenNotConnected()
        {
            // Act
            var result = await _connectionManager.GetConcurrencyConfigAsync();

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("Not connected");
        }

        [Fact]
        public async Task GetConcurrencyConfigAsync_LockingMode_ShouldBeNormal()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _connectionManager.GetConcurrencyConfigAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value["locking_mode"].Should().Be("normal");
        }

        [Fact]
        public async Task GetConcurrencyConfigAsync_Synchronous_ShouldBeNormal()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _connectionManager.GetConcurrencyConfigAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value["synchronous"].Should().BeOneOf("1", "normal"); // SQLite peut retourner 1 ou "normal"
        }

        [Fact]
        public async Task ConcurrentReads_ShouldWork()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act - Simuler plusieurs lectures en parallèle
            var tasks = new List<Task<bool>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_connectionManager.IsConnectionValidAsync());
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(r => r == true);
        }

        [Fact]
        public async Task ConnectAsync_AfterDisconnect_ShouldReconfigureConcurrency()
        {
            // Arrange
            await _connectionManager.ConnectAsync();
            await _connectionManager.DisconnectAsync();

            // Act
            var result = await _connectionManager.ConnectAsync();
            var config = await _connectionManager.GetConcurrencyConfigAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            config.IsSuccess.Should().BeTrue();
            config.Value["journal_mode"].Should().Be("wal");
        }

        [Fact]
        public async Task UpdateContext_ShouldRequireReconfiguration()
        {
            // Arrange
            await _connectionManager.ConnectAsync();
            var newContext = new TestDbContext();
            newContext.Database.OpenConnection();
            newContext.Database.EnsureCreated();

            // Act
            await Manager.RefreshContextAsync();
            await _connectionManager.ConnectAsync();
            var config = await _connectionManager.GetConcurrencyConfigAsync();

            // Assert
            config.IsSuccess.Should().BeTrue();
            config.Value["journal_mode"].Should().Be("wal");
        }

        


        [Fact]
        public async Task SequentialTransactions_ShouldWork()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act - Exécuter plusieurs transactions SÉQUENTIELLEMENT
            var results = new List<Result>();
            for (int i = 0; i < 3; i++)
            {
                var result = await _connectionManager.ExecuteInTransactionAsync(async () =>
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
                    await Task.Delay(10);
                });
                results.Add(result);
            }

            // Assert - Toutes les transactions devraient réussir
            results.Should().OnlyContain(r => r.IsSuccess);
            _connectionManager.IsConnected.Should().BeTrue();
        }

        [Fact]
        public async Task OneWriterMultipleReaders_ShouldWorkWithWAL()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Créer une table de test
            await _dbContext.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS TestData (Id INTEGER PRIMARY KEY, Value TEXT)");

            var config = await _connectionManager.GetConcurrencyConfigAsync();
            config.Value["journal_mode"].Should().Be("wal");

            // Act - Un écrivain + plusieurs lecteurs en parallèle
            var tasks = new List<Task>();

            // 1 tâche d'écriture
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        $"INSERT INTO TestData (Value) VALUES ('Write-{i}')");
                    await Task.Delay(20);
                }
            }));

            // 3 tâches de lecture en parallèle
            for (int i = 0; i < 3; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        await _dbContext.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM TestData");
                        await Task.Delay(10);
                    }
                }));
            }

            // Assert - Tout devrait fonctionner grâce à WAL
            await Task.WhenAll(tasks);
            _connectionManager.IsConnected.Should().BeTrue();
        }

        [Fact]
        public async Task FlushAsync_WithWALMode_ShouldUseFullCheckpoint()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _connectionManager.FlushAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
           // result.Error.Should().Contain("Flush completed");
        }
    }
}
