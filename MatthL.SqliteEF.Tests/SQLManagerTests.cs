using FluentAssertions;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Core.Models;
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
    /// Comprehensive tests for SQLManager functionality
    /// </summary>
    [Collection("SQLiteTests")]
    public class SQLManagerTests : IAsyncLifetime
    {
        private SQLManager _sqlManager;
        private string _testDbPath;
        private string _testDbName;

        public async Task InitializeAsync()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), "SQLiteTests");
            _testDbName = $"TestDb_{Guid.NewGuid()}";
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
                try
                {
                    Directory.Delete(_testDbPath, true);
                }
                catch { /* Ignore cleanup errors */ }
            }
        }

        #region Connection Management Tests

        [Fact]
        public async Task ConnectAsync_ShouldEstablishConnection()
        {
            // Arrange
            await _sqlManager.DisconnectAsync();

            // Act
            var result = await _sqlManager.ConnectAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            _sqlManager.IsConnected.Should().BeTrue();
            _sqlManager.CurrentState.Should().Be(ConnectionState.Connected);
            _sqlManager.LastConnection.Should().NotBeNull();
        }

        [Fact]
        public async Task ConnectAsync_WhenAlreadyConnected_ShouldReturnSuccess()
        {
            // Act
            var result = await _sqlManager.ConnectAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            _sqlManager.IsConnected.Should().BeTrue();
        }

        [Fact]
        public async Task DisconnectAsync_ShouldCloseConnection()
        {
            // Act
            var result = await _sqlManager.DisconnectAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            _sqlManager.IsConnected.Should().BeFalse();
            _sqlManager.CurrentState.Should().Be(ConnectionState.Disconnected);
        }

        [Fact]
        public async Task IsConnectionValidAsync_WhenConnected_ShouldReturnTrue()
        {
            // Act
            var isValid = await _sqlManager.IsConnectionValidAsync();

            // Assert
            isValid.Should().BeTrue();
            _sqlManager.LastActivity.Should().NotBeNull();
        }

        [Fact]
        public async Task IsConnectionValidAsync_WhenDisconnected_ShouldReturnFalse()
        {
            // Arrange
            await _sqlManager.DisconnectAsync();

            // Act
            var isValid = await _sqlManager.IsConnectionValidAsync();

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task GetConcurrencyConfigAsync_ShouldReturnWALConfiguration()
        {
            // Act
            var result = await _sqlManager.GetConcurrencyConfigAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainKey("journal_mode");
            result.Value["journal_mode"].Should().Be("wal");
            result.Value.Should().ContainKey("busy_timeout");
            result.Value["busy_timeout"].Should().Be("5000");
        }

        #endregion

        #region Health Check Tests

        [Fact]
        public async Task CheckHealthAsync_WhenConnected_ShouldReturnHealthy()
        {
            // Act
            var health = await _sqlManager.CheckHealthAsync();

            // Assert
            health.Should().NotBeNull();
            health.Status.Should().Be(HealthStatus.Healthy);
            health.Details.Should().ContainKey("pingMs");
            health.Details.Should().ContainKey("isConnected");
            health.Details["isConnected"].Should().Be(true);
        }

        [Fact]
        public async Task CheckHealthAsync_WhenDisconnected_ShouldReturnUnhealthy()
        {
            // Arrange
            await _sqlManager.DisconnectAsync();

            // Act
            var health = await _sqlManager.CheckHealthAsync();

            // Assert
            health.Status.Should().Be(HealthStatus.Unhealthy);
            health.Description.Should().Contain("disconnected");
        }

        [Fact]
        public async Task QuickHealthCheckAsync_ShouldBeFasterThanFullCheck()
        {
            // Act
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            await _sqlManager.QuickHealthCheckAsync();
            sw1.Stop();

            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            await _sqlManager.CheckHealthAsync();
            sw2.Stop();

            // Assert
            sw1.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(sw2.ElapsedMilliseconds);
        }

        [Fact]
        public async Task GetDatabaseStatisticsAsync_ShouldReturnValidStats()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 1 });

            // Act
            var result = await _sqlManager.GetDatabaseStatisticsAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.PageCount.Should().BeGreaterThan(0);
            result.Value.PageSize.Should().BeGreaterThan(0);
            result.Value.TableCount.Should().BeGreaterThan(0);
        }

        #endregion

        #region Database File Management Tests

        [Fact]
        public void GetDatabaseFileInfo_ShouldReturnFileInformation()
        {
            // Act
            var result = _sqlManager.GetDatabaseFileInfo();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.MainFileExists.Should().BeTrue();
            result.Value.FullPath.Should().Contain(_testDbName);
        }

        [Fact]
        public void GetFileSize_ShouldReturnPositiveValue()
        {
            // Act
            var fileSize = _sqlManager.GetFileSize;

            // Assert
            fileSize.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SetPaths_ShouldUpdateDatabasePaths()
        {
            // Arrange
            var newPath = Path.Combine(Path.GetTempPath(), "NewTestPath");
            var newName = "NewDatabase";

            // Act
            _sqlManager.SetPaths(newPath, newName, ".db");

            // Assert
            _sqlManager.GetFolderPath.Should().Be(newPath);
            _sqlManager.GetFileName.Should().Be(newName);
            _sqlManager.GetFullPath.Should().Be(Path.Combine(newPath, newName + ".db"));
        }

        [Fact]
        public async Task FlushAsync_ShouldCompleteSuccessfully()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 1 });

            // Act
            var result = await _sqlManager.FlushAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        #endregion

        #region CRUD Operations Tests

        [Fact]
        public async Task AddAsync_ShouldAddEntity()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test", Value = 123 };

            // Act
            var result = await _sqlManager.AddAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            entity.Id.Should().BeGreaterThan(0);

            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            retrieved.IsSuccess.Should().BeTrue();
            retrieved.Value.Name.Should().Be("Test");
        }

        [Fact]
        public async Task AddAsync_WithNullEntity_ShouldFail()
        {
            // Act
            var result = await _sqlManager.AddAsync<TestEntity>(null);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("null");
        }

        [Fact]
        public async Task AddRangeAsync_ShouldAddMultipleEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity1", Value = 1 },
                new TestEntity { Name = "Entity2", Value = 2 },
                new TestEntity { Name = "Entity3", Value = 3 }
            };

            // Act
            var result = await _sqlManager.AddRangeAsync(entities);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var allEntities = await _sqlManager.GetAllAsync<TestEntity>();
            allEntities.Value.Should().HaveCountGreaterThanOrEqualTo(3);
        }

        [Fact]
        public async Task UpdateAsync_ShouldModifyEntity()
        {
            // Arrange
            var entity = new TestEntity { Name = "Original", Value = 1 };
            await _sqlManager.AddAsync(entity);

            // Act
            entity.Name = "Modified";
            entity.Value = 999;
            var result = await _sqlManager.UpdateAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            retrieved.Value.Name.Should().Be("Modified");
            retrieved.Value.Value.Should().Be(999);
        }

        [Fact]
        public async Task UpdateRangeAsync_ShouldUpdateMultipleEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity1", Value = 1 },
                new TestEntity { Name = "Entity2", Value = 2 }
            };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            entities[0].Value = 10;
            entities[1].Value = 20;
            var result = await _sqlManager.UpdateRangeAsync(entities);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var retrieved = await _sqlManager.GetAllAsync<TestEntity>();
            retrieved.Value.Should().Contain(e => e.Value == 10);
            retrieved.Value.Should().Contain(e => e.Value == 20);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveEntity()
        {
            // Arrange
            var entity = new TestEntity { Name = "ToDelete", Value = 1 };
            await _sqlManager.AddAsync(entity);

            // Act
            var result = await _sqlManager.DeleteAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            retrieved.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteRangeAsync_ShouldRemoveMultipleEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Delete1", Value = 1 },
                new TestEntity { Name = "Delete2", Value = 2 },
                new TestEntity { Name = "Keep", Value = 3 }
            };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var toDelete = entities.Take(2).ToList();
            var result = await _sqlManager.DeleteRangeAsync(toDelete);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var remaining = await _sqlManager.GetAllAsync<TestEntity>();
            remaining.Value.Should().HaveCount(1);
            remaining.Value[0].Name.Should().Be("Keep");
        }

        [Fact]
        public async Task DeleteAllAsync_ShouldRemoveAllEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity1", Value = 1 },
                new TestEntity { Name = "Entity2", Value = 2 }
            };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var result = await _sqlManager.DeleteAllAsync<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            var remaining = await _sqlManager.GetAllAsync<TestEntity>();
            remaining.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task AddOrUpdateAsync_WithNewEntity_ShouldAdd()
        {
            // Arrange
            var entity = new TestEntity { Name = "New", Value = 42 };

            // Act
            var result = await _sqlManager.AddOrUpdateAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            entity.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task AddOrUpdateAsync_WithExistingEntity_ShouldUpdate()
        {
            // Arrange
            var entity = new TestEntity { Name = "Original", Value = 1 };
            await _sqlManager.AddAsync(entity);

            // Act
            entity.Name = "Updated";
            entity.Value = 999;
            var result = await _sqlManager.AddOrUpdateAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            retrieved.Value.Name.Should().Be("Updated");
            retrieved.Value.Value.Should().Be(999);
        }

        #endregion

        #region Read Operations Tests

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllEntities()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(new List<TestEntity>
            {
                new TestEntity { Name = "Entity1", Value = 1 },
                new TestEntity { Name = "Entity2", Value = 2 }
            });

            // Act
            var result = await _sqlManager.GetAllAsync<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCountGreaterThanOrEqualTo(2);
        }

        [Fact]
        public async Task GetByIdAsync_WithValidId_ShouldReturnEntity()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test", Value = 123 };
            await _sqlManager.AddAsync(entity);

            // Act
            var result = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Name.Should().Be("Test");
        }

        [Fact]
        public async Task GetByIdAsync_WithInvalidId_ShouldFail()
        {
            // Act
            var result = await _sqlManager.GetByIdAsync<TestEntity>(99999);

            // Assert
            result.IsSuccess.Should().BeFalse();
        }

        [Fact]
        public async Task WhereAsync_ShouldFilterEntities()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(new List<TestEntity>
            {
                new TestEntity { Name = "High", Value = 100 },
                new TestEntity { Name = "Low", Value = 10 }
            });

            // Act
            var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > 50);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(1);
            result.Value[0].Name.Should().Be("High");
        }

        [Fact]
        public async Task AnyAsync_WithMatchingPredicate_ShouldReturnTrue()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 123 });

            // Act
            var result = await _sqlManager.AnyAsync<TestEntity>(e => e.Value == 123);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
        }

        [Fact]
        public async Task AnyExistAsync_WithEntities_ShouldReturnTrue()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 1 });

            // Act
            var result = await _sqlManager.AnyExistAsync<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
        }

        [Fact]
        public async Task CountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(new List<TestEntity>
            {
                new TestEntity { Name = "Entity1", Value = 1 },
                new TestEntity { Name = "Entity2", Value = 2 },
                new TestEntity { Name = "Entity3", Value = 3 }
            });

            // Act
            var result = await _sqlManager.CountAsync<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeGreaterThanOrEqualTo(3);
        }

        [Fact]
        public async Task CountAsync_WithPredicate_ShouldReturnFilteredCount()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(new List<TestEntity>
            {
                new TestEntity { Name = "High", Value = 100 },
                new TestEntity { Name = "Low", Value = 10 }
            });

            // Act
            var result = await _sqlManager.CountAsync<TestEntity>(e => e.Value > 50);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(1);
        }

        [Fact]
        public async Task FirstOrDefaultAsync_WithMatchingPredicate_ShouldReturnEntity()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Target", Value = 123 });

            // Act
            var result = await _sqlManager.FirstOrDefaultAsync<TestEntity>(e => e.Name == "Target");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be(123);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnCorrectPage()
        {
            // Arrange
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 50; i++)
            {
                entities.Add(new TestEntity { Name = $"Entity{i}", Value = i });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var result = await _sqlManager.GetPagedAsync<TestEntity>(
                pageNumber: 2,
                pageSize: 10,
                orderBy: e => e.Value,
                ascending: true
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(10);
            result.Value[0].Value.Should().Be(11);
        }

        [Fact]
        public async Task ExecuteQueryAsync_ShouldExecuteCustomQuery()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(new List<TestEntity>
            {
                new TestEntity { Name = "Test1", Value = 1 },
                new TestEntity { Name = "Test2", Value = 2 }
            });

            // Act
            var result = await _sqlManager.ExecuteQueryAsync<TestEntity>(
                query => query.Where(e => e.Name.StartsWith("Test")).OrderBy(e => e.Value)
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCountGreaterThanOrEqualTo(2);
        }

        [Fact]
        public async Task ExecuteQueryNoTrackingAsync_ShouldReturnUntracked()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 1 });

            // Act
            var result = await _sqlManager.ExecuteQueryNoTrackingAsync<TestEntity>(
                query => query.Where(e => e.Name == "Test")
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(1);
        }

        [Fact]
        public async Task ExecuteSingleQueryAsync_ShouldReturnSingleEntity()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Single", Value = 123 });

            // Act
            var result = await _sqlManager.ExecuteSingleQueryAsync<TestEntity>(
                query => query.Where(e => e.Name == "Single")
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Value.Should().Be(123);
        }

        [Fact]
        public async Task ExecuteAggregateAsync_ShouldReturnAggregateResult()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(new List<TestEntity>
            {
                new TestEntity { Name = "Test1", Value = 10 },
                new TestEntity { Name = "Test2", Value = 20 },
                new TestEntity { Name = "Test3", Value = 30 }
            });

            // Act
            var result = await _sqlManager.ExecuteAggregateAsync<TestEntity, int>(
                query => query.SumAsync(e => e.Value)
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeGreaterThanOrEqualTo(60);
        }

        #endregion

        #region Transaction Tests

        [Fact]
        public async Task ExecuteInTransactionAsync_ShouldCommitSuccessfully()
        {
            // Act
            var result = await _sqlManager.ExecuteInTransactionAsync(async context =>
            {
                context.Set<TestEntity>().Add(new TestEntity { Name = "Transaction", Value = 1 });
                await context.SaveChangesAsync();
            });

            // Assert
            result.IsSuccess.Should().BeTrue();
            var entities = await _sqlManager.WhereAsync<TestEntity>(e => e.Name == "Transaction");
            entities.Value.Should().HaveCount(1);
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_WithException_ShouldRollback()
        {
            // Act
            var result = await _sqlManager.ExecuteInTransactionAsync(async context =>
            {
                context.Set<TestEntity>().Add(new TestEntity { Name = "WillFail", Value = 1 });
                await context.SaveChangesAsync();
                throw new Exception("Test exception");
            });

            // Assert
            result.IsSuccess.Should().BeFalse();
            var entities = await _sqlManager.WhereAsync<TestEntity>(e => e.Name == "WillFail");
            entities.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_WithResult_ShouldReturnValue()
        {
            // Act
            var result = await _sqlManager.ExecuteInTransactionAsync(async context =>
            {
                var entity = new TestEntity { Name = "WithResult", Value = 42 };
                context.Set<TestEntity>().Add(entity);
                await context.SaveChangesAsync();
                return entity.Id;
            });

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ExecuteBatchInTransactionAsync_ShouldCommitBatch()
        {
            // Act
            var result = await _sqlManager.ExecuteBatchInTransactionAsync<TestEntity>(
                async (context, set) =>
                {
                    set.Add(new TestEntity { Name = "Batch1", Value = 1 });
                    set.Add(new TestEntity { Name = "Batch2", Value = 2 });
                    await context.SaveChangesAsync();
                }
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            var count = await _sqlManager.CountAsync<TestEntity>(e => e.Name.StartsWith("Batch"));
            count.Value.Should().Be(2);
        }

        [Fact]
        public async Task ExecuteReadTransactionAsync_ShouldMaintainConsistency()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(new List<TestEntity>
            {
                new TestEntity { Name = "Read1", Value = 1 },
                new TestEntity { Name = "Read2", Value = 2 }
            });

            // Act
            var result = await _sqlManager.ExecuteReadTransactionAsync(async context =>
            {
                var entities = await context.Set<TestEntity>()
                    .Where(e => e.Name.StartsWith("Read"))
                    .ToListAsync();
                return entities.Count;
            });

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(2);
        }

        [Fact]
        public async Task ExecuteComplexTransactionAsync_ShouldHandleMultipleOperations()
        {
            // Act
            var result = await _sqlManager.ExecuteComplexTransactionAsync(
                async context =>
                {
                    var entity = new TestEntity { Name = "Complex", Value = 1 };
                    context.Set<TestEntity>().Add(entity);
                    await context.SaveChangesAsync();

                    entity.Value = 100;
                    await context.SaveChangesAsync();

                    return entity.Id;
                },
                typeof(TestEntity)
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(result.Value);
            retrieved.Value.Value.Should().Be(100);
        }

        #endregion

        #region Concurrency Tests

        [Fact]
        public async Task ConcurrentReads_ShouldAllSucceed()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 100)
                .Select(i => new TestEntity { Name = $"Concurrent{i}", Value = i })
                .ToList());

            // Act
            var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
            {
                var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > i * 10);
                return result.IsSuccess;
            }));

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(r => r == true);
        }

        [Fact]
        public async Task ConcurrentWrites_ShouldAllSucceed()
        {
            // Act
            var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
            {
                var entity = new TestEntity { Name = $"Write{i}", Value = i };
                var result = await _sqlManager.AddAsync(entity);
                return result.IsSuccess;
            }));

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(r => r == true);
            var count = await _sqlManager.CountAsync<TestEntity>(e => e.Name.StartsWith("Write"));
            count.Value.Should().Be(10);
        }

        [Fact]
        public async Task ConcurrentReadsAndWrites_ShouldWork()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 50)
                .Select(i => new TestEntity { Name = $"Initial{i}", Value = i })
                .ToList());

            // Act
            var readTasks = Enumerable.Range(1, 5).Select(i => Task.Run(async () =>
            {
                for (int j = 0; j < 10; j++)
                {
                    var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > i * 5);
                    if (!result.IsSuccess) return false;
                    await Task.Delay(5);
                }
                return true;
            }));

            var writeTasks = Enumerable.Range(1, 3).Select(i => Task.Run(async () =>
            {
                for (int j = 0; j < 5; j++)
                {
                    var entity = new TestEntity { Name = $"Concurrent{i}-{j}", Value = 1000 + i * 10 + j };
                    var result = await _sqlManager.AddAsync(entity);
                    if (!result.IsSuccess) return false;
                    await Task.Delay(10);
                }
                return true;
            }));

            var allResults = await Task.WhenAll(readTasks.Concat(writeTasks));

            // Assert
            allResults.Should().OnlyContain(r => r == true);
        }

        [Fact]
        public async Task ConcurrentPagination_ShouldReturnConsistentResults()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 1000)
                .Select(i => new TestEntity { Name = $"Page{i}", Value = i })
                .ToList());

            // Act
            var tasks = Enumerable.Range(1, 10).Select(page => Task.Run(async () =>
            {
                var result = await _sqlManager.GetPagedAsync<TestEntity>(
                    pageNumber: page,
                    pageSize: 100,
                    orderBy: e => e.Value,
                    ascending: true
                );
                return result.IsSuccess ? result.Value.Count : 0;
            }));

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Sum().Should().Be(1000);
            results.Should().OnlyContain(count => count == 100);
        }

        [Fact]
        public async Task ConcurrentTransactions_ShouldMaintainIsolation()
        {
            // Act
            var tasks = Enumerable.Range(1, 5).Select(i => Task.Run(async () =>
            {
                return await _sqlManager.ExecuteInTransactionAsync(async context =>
                {
                    var entity = new TestEntity { Name = $"Transaction{i}", Value = i };
                    context.Set<TestEntity>().Add(entity);
                    await context.SaveChangesAsync();
                    await Task.Delay(50); // Simulate work
                });
            }));

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(r => r.IsSuccess);
            var count = await _sqlManager.CountAsync<TestEntity>(e => e.Name.StartsWith("Transaction"));
            count.Value.Should().Be(5);
        }

        [Fact]
        public async Task HighVolumeConcurrentOperations_ShouldHandleLoad()
        {
            // Act - 50 concurrent operations mixing reads and writes
            var tasks = new List<Task<bool>>();

            for (int i = 0; i < 30; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var result = await _sqlManager.AddAsync(new TestEntity { Name = $"Load{index}", Value = index });
                    return result.IsSuccess;
                }));
            }

            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var result = await _sqlManager.GetAllAsync<TestEntity>();
                    return result.IsSuccess;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(r => r == true);
            var finalCount = await _sqlManager.CountAsync<TestEntity>(e => e.Name.StartsWith("Load"));
            finalCount.Value.Should().Be(30);
        }

        [Fact]
        public async Task ConcurrentHealthChecks_DuringOperations_ShouldNotInterfere()
        {
            // Arrange
            var operationTasks = new List<Task>();
            var healthCheckTasks = new List<Task<HealthCheckResult>>();

            // Act - Operations
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                operationTasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        await _sqlManager.AddAsync(new TestEntity { Name = $"Health{index}-{j}", Value = j });
                        await Task.Delay(10);
                    }
                }));
            }

            // Act - Health checks
            for (int i = 0; i < 10; i++)
            {
                healthCheckTasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(Random.Shared.Next(5, 50));
                    return await _sqlManager.CheckHealthAsync();
                }));
            }

            await Task.WhenAll(operationTasks);
            var healthResults = await Task.WhenAll(healthCheckTasks);

            // Assert
            healthResults.Should().OnlyContain(h => h.Status != HealthStatus.Unhealthy);
            var count = await _sqlManager.CountAsync<TestEntity>(e => e.Name.StartsWith("Health"));
            count.Value.Should().Be(50);
        }

        [Fact]
        public async Task StressTest_MassiveConcurrentLoad_ShouldRemainStable()
        {
            // Arrange - This is a stress test with 100+ concurrent operations
            var tasks = new List<Task<bool>>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (index % 3 == 0)
                        {
                            // Write operation
                            var result = await _sqlManager.AddAsync(new TestEntity { Name = $"Stress{index}", Value = index });
                            return result.IsSuccess;
                        }
                        else if (index % 3 == 1)
                        {
                            // Read operation
                            var result = await _sqlManager.GetAllAsync<TestEntity>();
                            return result.IsSuccess;
                        }
                        else
                        {
                            // Query operation
                            var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value < index);
                            return result.IsSuccess;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - At least 95% success rate under stress
            var successRate = results.Count(r => r) / (double)results.Length;
            successRate.Should().BeGreaterThan(0.95);
            _sqlManager.IsConnected.Should().BeTrue();
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void IsSavable_WithNewEntity_ShouldCheckCreatePermission()
        {
            // Arrange
            var entity = new TestEntity { Name = "New", Value = 1 };

            // Act
            var result = _sqlManager.IsSavable(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue(); // AdminAuthorization allows everything
        }

        [Fact]
        public void IsSavable_WithExistingEntity_ShouldCheckUpdatePermission()
        {
            // Arrange
            var entity = new TestEntity { Id = 1, Name = "Existing", Value = 1 };

            // Act
            var result = _sqlManager.IsSavable(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
        }

        #endregion
    }

    #region Test Infrastructure

    public class TestEntity : IBaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
        public void ConfigureEntity(ModelBuilder modelBuilder)
        {

        }
    }

    public class TestDbContext : RootDbContext
    {
        public DbSet<TestEntity> TestEntities { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite("DataSource=:memory:");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestEntity>().ToTable("TestEntities");
        }
    }

    public class TestDbContextFactory : IDbContextFactory<RootDbContext>
    {
        public RootDbContext CreateDbContext()
        {
            return new TestDbContext();
        }
    }


    #endregion
}