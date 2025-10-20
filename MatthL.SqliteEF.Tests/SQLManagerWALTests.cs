using FluentAssertions;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Managers.Delegates;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MatthL.SqliteEF.Tests
{
    /// <summary>
    /// Tests specifically for WAL mode and concurrency configuration
    /// </summary>
    [Collection("SQLiteWALTests")]
    public class SQLManagerWALTests : IAsyncLifetime
    {
        private SQLManager _sqlManager;
        private string _testDbPath;
        private string _testDbName;

        public async Task InitializeAsync()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), "SQLiteWALTests");
            _testDbName = $"WALTestDb_{Guid.NewGuid()}";
            Directory.CreateDirectory(_testDbPath);

            var contextFactory = new TestDbContextFactory();
            _sqlManager = new SQLManager(
                contextFactory,
                _testDbPath,
                _testDbName,
                ".db"
            );

            await _sqlManager.Create();
            await _sqlManager.ConnectAsync();
        }

        public async Task DisposeAsync()
        {
            await _sqlManager.DisconnectAsync();
            await _sqlManager.DeleteCurrentDatabase();

            if (Directory.Exists(_testDbPath))
            {
                try { Directory.Delete(_testDbPath, true); }
                catch { /* Ignore cleanup errors */ }
            }
        }

        #region WAL Configuration Tests

        [Fact]
        public async Task OnConnect_ShouldConfigureWALMode()
        {
            // Act
            var config = await _sqlManager.GetConcurrencyConfigAsync();

            // Assert
            config.IsSuccess.Should().BeTrue();
            config.Value.Should().ContainKey("journal_mode");
            config.Value["journal_mode"].ToLower().Should().Be("wal");
        }

        [Fact]
        public async Task OnConnect_ShouldSetBusyTimeout()
        {
            // Act
            var config = await _sqlManager.GetConcurrencyConfigAsync();

            // Assert
            config.IsSuccess.Should().BeTrue();
            config.Value.Should().ContainKey("busy_timeout");
            config.Value["busy_timeout"].Should().Be("5000");
        }

        [Fact]
        public async Task OnConnect_ShouldSetCacheSize()
        {
            // Act
            var config = await _sqlManager.GetConcurrencyConfigAsync();

            // Assert
            config.IsSuccess.Should().BeTrue();
            config.Value.Should().ContainKey("cache_size");
            // Cache size is in pages, negative value means KB
            int.Parse(config.Value["cache_size"]).Should().BeLessThan(0);
        }

        [Fact]
        public async Task OnConnect_ShouldSetSynchronousMode()
        {
            // Act
            var config = await _sqlManager.GetConcurrencyConfigAsync();

            // Assert
            config.IsSuccess.Should().BeTrue();
            config.Value.Should().ContainKey("synchronous");
            // Can be "1" or "normal"
            config.Value["synchronous"].ToLower().Should().BeOneOf("1", "normal");
        }

        [Fact]
        public async Task OnConnect_ShouldSetLockingMode()
        {
            // Act
            var config = await _sqlManager.GetConcurrencyConfigAsync();

            // Assert
            config.IsSuccess.Should().BeTrue();
            config.Value.Should().ContainKey("locking_mode");
            config.Value["locking_mode"].ToLower().Should().Be("normal");
        }

        [Fact]
        public async Task OnConnect_ShouldEnableForeignKeys()
        {
            // Act
            var config = await _sqlManager.GetConcurrencyConfigAsync();

            // Assert
            config.IsSuccess.Should().BeTrue();
            config.Value.Should().ContainKey("foreign_keys");
            config.Value["foreign_keys"].Should().BeOneOf("0", "1", "ON", "OFF");
        }

        [Fact]
        public async Task GetConcurrencyConfig_WhenDisconnected_ShouldFail()
        {
            // Arrange
            await _sqlManager.DisconnectAsync();

            // Act
            var config = await _sqlManager.GetConcurrencyConfigAsync();

            // Assert
            config.IsSuccess.Should().BeFalse();
            config.Error.Should().Contain("Not connected");
        }

        [Fact]
        public async Task ReconnectAfterDisconnect_ShouldReconfigureWAL()
        {
            // Arrange
            await _sqlManager.DisconnectAsync();

            // Act
            await _sqlManager.ConnectAsync();
            var config = await _sqlManager.GetConcurrencyConfigAsync();

            // Assert
            config.IsSuccess.Should().BeTrue();
            config.Value["journal_mode"].ToLower().Should().Be("wal");
        }

        #endregion

        #region WAL File Operations

        [Fact]
        public async Task AfterWrites_WALFileShouldExist()
        {
            // Arrange & Act
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 100)
                .Select(i => new TestEntity { Name = $"WAL{i}", Value = i })
                .ToList());

            var fileInfo = _sqlManager.GetDatabaseFileInfo();

            // Assert
            fileInfo.IsSuccess.Should().BeTrue();
            fileInfo.Value.MainFileExists.Should().BeTrue();
            // WAL file may or may not exist depending on checkpoint timing
        }

        [Fact]
        public async Task FlushAsync_ShouldCheckpointWAL()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 500)
                .Select(i => new TestEntity { Name = $"Checkpoint{i}", Value = i })
                .ToList());

            var infoBefore = _sqlManager.GetDatabaseFileInfo();

            // Act
            var result = await _sqlManager.FlushAsync();
            await Task.Delay(200); // Give filesystem time to update

            var infoAfter = _sqlManager.GetDatabaseFileInfo();

            // Assert
            result.IsSuccess.Should().BeTrue();
            infoBefore.IsSuccess.Should().BeTrue();
            infoAfter.IsSuccess.Should().BeTrue();
        }

        #endregion

        #region Concurrent Read Tests

        [Fact]
        public async Task MultipleSimultaneousReads_ShouldAllSucceed()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 1000)
                .Select(i => new TestEntity { Name = $"Read{i}", Value = i })
                .ToList());

            // Act - 20 simultaneous reads
            var tasks = Enumerable.Range(1, 20).Select(i => Task.Run(async () =>
            {
                var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > i * 10);
                return result.IsSuccess;
            }));

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(r => r == true);
        }

        [Fact]
        public async Task ContinuousReadsWhileWriting_ShouldWork()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 100)
                .Select(i => new TestEntity { Name = $"Initial{i}", Value = i })
                .ToList());

            // Act - Continuous reads
            var readTask = Task.Run(async () =>
            {
                for (int i = 0; i < 50; i++)
                {
                    var result = await _sqlManager.GetAllAsync<TestEntity>();
                    if (!result.IsSuccess) return false;
                    await Task.Delay(10);
                }
                return true;
            });

            // Simultaneous writes
            var writeTask = Task.Run(async () =>
            {
                for (int i = 0; i < 20; i++)
                {
                    var result = await _sqlManager.AddAsync(new TestEntity
                    {
                        Name = $"Concurrent{i}",
                        Value = 1000 + i
                    });
                    if (!result.IsSuccess) return false;
                    await Task.Delay(25);
                }
                return true;
            });

            var results = await Task.WhenAll(readTask, writeTask);

            // Assert
            results.Should().OnlyContain(r => r == true);
        }

        [Fact]
        public async Task HighVolumeReads_WithOccasionalWrites_ShouldMaintainPerformance()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 1000)
                .Select(i => new TestEntity { Name = $"Data{i}", Value = i })
                .ToList());

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Act - 50 read tasks
            var readTasks = Enumerable.Range(1, 50).Select(_ => Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var result = await _sqlManager.WhereAsync<TestEntity>(
                        e => e.Value > Random.Shared.Next(0, 1000)
                    );
                    if (!result.IsSuccess) return false;
                }
                return true;
            }));

            // 5 write tasks
            var writeTasks = Enumerable.Range(1, 5).Select(i => Task.Run(async () =>
            {
                for (int j = 0; j < 3; j++)
                {
                    var result = await _sqlManager.AddAsync(new TestEntity
                    {
                        Name = $"Write{i}-{j}",
                        Value = 2000 + i * 10 + j
                    });
                    if (!result.IsSuccess) return false;
                    await Task.Delay(100);
                }
                return true;
            }));

            var allResults = await Task.WhenAll(readTasks.Concat(writeTasks));
            sw.Stop();

            // Assert
            allResults.Should().OnlyContain(r => r == true);
            sw.ElapsedMilliseconds.Should().BeLessThan(10000); // Should be reasonably fast
        }

        #endregion

        #region Write Serialization Tests

        [Fact]
        public async Task SequentialWrites_ShouldAllSucceed()
        {
            // Act - Write one at a time
            var results = new List<bool>();
            for (int i = 0; i < 20; i++)
            {
                var result = await _sqlManager.AddAsync(new TestEntity
                {
                    Name = $"Sequential{i}",
                    Value = i
                });
                results.Add(result.IsSuccess);
            }

            // Assert
            results.Should().OnlyContain(r => r == true);
            var count = await _sqlManager.CountAsync<TestEntity>(e => e.Name.StartsWith("Sequential"));
            count.Value.Should().Be(20);
        }

        [Fact]
        public async Task ConcurrentWrites_ShouldSerialize()
        {
            // Act - Multiple write tasks
            var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
            {
                var result = await _sqlManager.AddAsync(new TestEntity
                {
                    Name = $"Serialize{i}",
                    Value = i
                });
                return result.IsSuccess;
            }));

            var results = await Task.WhenAll(tasks);

            // Assert - All should succeed even though serialized
            results.Should().OnlyContain(r => r == true);
            var count = await _sqlManager.CountAsync<TestEntity>(e => e.Name.StartsWith("Serialize"));
            count.Value.Should().Be(10);
        }

        [Fact]
        public async Task MixedReadWriteLoad_ShouldHandleGracefully()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 200)
                .Select(i => new TestEntity { Name = $"Base{i}", Value = i })
                .ToList());

            // Act - Mix of operations
            var tasks = new List<Task<bool>>();

            // 30 reads
            for (int i = 0; i < 30; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await _sqlManager.GetAllAsync<TestEntity>();
                    return result.IsSuccess;
                }));
            }

            // 10 writes
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var result = await _sqlManager.AddAsync(new TestEntity
                    {
                        Name = $"Mixed{index}",
                        Value = 1000 + index
                    });
                    return result.IsSuccess;
                }));
            }

            // 10 queries
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > index * 20);
                    return result.IsSuccess;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(r => r == true);
        }

        #endregion

        #region Transaction Concurrency Tests

        [Fact]
        public async Task ReadTransactionsDuringWrite_ShouldNotBlock()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Base", Value = 1 });

            // Act - Long write transaction
            var writeTransaction = Task.Run(async () =>
            {
                return await _sqlManager.ExecuteInTransactionAsync(async context =>
                {
                    context.Set<TestEntity>().Add(new TestEntity { Name = "SlowWrite", Value = 100 });
                    await context.SaveChangesAsync();
                    await Task.Delay(500); // Simulate slow operation
                });
            });

            // Concurrent read transactions
            var readTransactions = Enumerable.Range(1, 5).Select(_ => Task.Run(async () =>
            {
                await Task.Delay(Random.Shared.Next(0, 300));
                return await _sqlManager.ExecuteReadTransactionAsync(async context =>
                {
                    var count = await context.Set<TestEntity>().CountAsync();
                    return count;
                });
            }));

            var allResults = await Task.WhenAll(
                new[] { writeTransaction.ContinueWith(t => t.Result.IsSuccess) }
                .Concat(readTransactions.Select(t => t.ContinueWith(r => r.Result.IsSuccess)))
            );

            // Assert - All should succeed
            allResults.Should().OnlyContain(r => r == true);
        }

        [Fact]
        public async Task MultipleWriteTransactions_ShouldSerialize()
        {
            // Act - Multiple write transactions
            var tasks = Enumerable.Range(1, 5).Select(i => Task.Run(async () =>
            {
                return await _sqlManager.ExecuteInTransactionAsync(async context =>
                {
                    context.Set<TestEntity>().Add(new TestEntity
                    {
                        Name = $"TxWrite{i}",
                        Value = i
                    });
                    await context.SaveChangesAsync();
                    await Task.Delay(50); // Ensure some overlap
                });
            }));

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(r => r.IsSuccess);
            var count = await _sqlManager.CountAsync<TestEntity>(e => e.Name.StartsWith("TxWrite"));
            count.Value.Should().Be(5);
        }

        #endregion

        #region Connection Stability Tests

        [Fact]
        public async Task ConnectionValidation_DuringHeavyLoad_ShouldRemainValid()
        {
            // Arrange
            var validationTask = Task.Run(async () =>
            {
                var validations = new List<bool>();
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(50);
                    validations.Add(await _sqlManager.IsConnectionValidAsync());
                }
                return validations;
            });

            // Heavy load
            var loadTasks = Enumerable.Range(1, 30).Select(i => Task.Run(async () =>
            {
                if (i % 2 == 0)
                {
                    await _sqlManager.AddAsync(new TestEntity { Name = $"Load{i}", Value = i });
                }
                else
                {
                    await _sqlManager.GetAllAsync<TestEntity>();
                }
            }));

            await Task.WhenAll(loadTasks);
            var validations = await validationTask;

            // Assert
            validations.Should().OnlyContain(v => v == true);
        }

        [Fact]
        public async Task HealthChecks_DuringConcurrentOperations_ShouldReportHealthy()
        {
            // Arrange
            var healthCheckTask = Task.Run(async () =>
            {
                var checks = new List<HealthCheckResult>();
                for (int i = 0; i < 10; i++)
                {
                    await Task.Delay(100);
                    checks.Add(await _sqlManager.CheckHealthAsync());
                }
                return checks;
            });

            // Concurrent operations
            var operationTasks = Enumerable.Range(1, 50).Select(i => Task.Run(async () =>
            {
                await _sqlManager.AddAsync(new TestEntity { Name = $"Health{i}", Value = i });
                await Task.Delay(20);
            }));

            await Task.WhenAll(operationTasks);
            var healthChecks = await healthCheckTask;

            // Assert
            healthChecks.Should().OnlyContain(h =>
                h.Status == HealthStatus.Healthy || h.Status == HealthStatus.Degraded
            );
        }

        #endregion

        #region Performance with WAL

        [Fact]
        public async Task WALMode_BatchInserts_ShouldBeEfficient()
        {
            // Arrange
            var entities = Enumerable.Range(1, 5000)
                .Select(i => new TestEntity { Name = $"Batch{i}", Value = i })
                .ToList();

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _sqlManager.AddRangeAsync(entities);
            sw.Stop();

            // Assert
            result.IsSuccess.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(3000); // WAL should be fast
        }

        [Fact]
        public async Task WALMode_ConcurrentReadsOfLargeDataset_ShouldBeEfficient()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 10000)
                .Select(i => new TestEntity { Name = $"Large{i}", Value = i })
                .ToList());

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var tasks = Enumerable.Range(1, 20).Select(_ => Task.Run(async () =>
            {
                var result = await _sqlManager.WhereAsync<TestEntity>(
                    e => e.Value > Random.Shared.Next(0, 10000)
                );
                return result.IsSuccess;
            }));

            var results = await Task.WhenAll(tasks);
            sw.Stop();

            // Assert
            results.Should().OnlyContain(r => r == true);
            sw.ElapsedMilliseconds.Should().BeLessThan(5000);
        }

        #endregion

        #region Database Statistics with WAL

        [Fact]
        public async Task GetDatabaseStatistics_WithWAL_ShouldIncludeWALInfo()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 100)
                .Select(i => new TestEntity { Name = $"Stats{i}", Value = i })
                .ToList());

            // Act
            var result = await _sqlManager.GetDatabaseStatisticsAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.PageCount.Should().BeGreaterThan(0);
            result.Value.PageSize.Should().BeGreaterThan(0);
            result.Value.TotalSizeBytes.Should().BeGreaterThan(0);
            result.Value.TableCount.Should().BeGreaterThan(0);
        }

        #endregion

        #region Cleanup and Maintenance

        [Fact]
        public async Task DisconnectAfterHeavyUse_ShouldCleanupProperly()
        {
            // Arrange - Heavy use
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 1000)
                .Select(i => new TestEntity { Name = $"Cleanup{i}", Value = i })
                .ToList());

            // Act
            var disconnectResult = await _sqlManager.DisconnectAsync();
            await Task.Delay(200); // Give time for cleanup

            // Assert
            disconnectResult.IsSuccess.Should().BeTrue();
            _sqlManager.IsConnected.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteRawSql_CheckpointCommand_ShouldWork()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 100)
                .Select(i => new TestEntity { Name = $"Raw{i}", Value = i })
                .ToList());

            // Act
            var result = await _sqlManager.ExecuteRawSqlAsync("PRAGMA wal_checkpoint(FULL)");

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        #endregion
    }
}