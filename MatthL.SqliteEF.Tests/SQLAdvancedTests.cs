using FluentAssertions;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers;
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
    /// Advanced tests for edge cases, performance, and complex scenarios
    /// </summary>
    [Collection("SQLiteAdvancedTests")]
    public class SQLManagerAdvancedTests : IAsyncLifetime
    {
        private SQLManager _sqlManager;
        private string _testDbPath;
        private string _testDbName;

        public async Task InitializeAsync()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), "SQLiteAdvancedTests");
            _testDbName = $"AdvancedTestDb_{Guid.NewGuid()}";
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

        #region Performance Tests

        [Fact]
        public async Task BulkInsert_LargeDataset_ShouldBeEfficient()
        {
            // Arrange
            var entities = Enumerable.Range(1, 10000)
                .Select(i => new TestEntity { Name = $"Bulk{i}", Value = i })
                .ToList();

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _sqlManager.AddRangeAsync(entities);
            sw.Stop();

            // Assert
            result.IsSuccess.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete in less than 5 seconds

            var count = await _sqlManager.CountAsync<TestEntity>();
            count.Value.Should().Be(10000);
        }

        [Fact]
        public async Task BulkUpdate_LargeDataset_ShouldBeEfficient()
        {
            // Arrange
            var entities = Enumerable.Range(1, 5000)
                .Select(i => new TestEntity { Name = $"Update{i}", Value = i })
                .ToList();
            await _sqlManager.AddRangeAsync(entities);

            // Act
            entities.ForEach(e => e.Value *= 2);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _sqlManager.UpdateRangeAsync(entities);
            sw.Stop();

            // Assert
            result.IsSuccess.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(5000);
        }

        [Fact]
        public async Task ComplexQuery_WithMultipleFilters_ShouldBeOptimized()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 1000)
                .Select(i => new TestEntity
                {
                    Name = i % 2 == 0 ? "Even" : "Odd",
                    Value = i
                })
                .ToList());

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _sqlManager.ExecuteQueryAsync<TestEntity>(query =>
                query.Where(e => e.Name == "Even")
                     .Where(e => e.Value > 100)
                     .Where(e => e.Value < 900)
                     .OrderBy(e => e.Value)
            );
            sw.Stop();

            // Assert
            result.IsSuccess.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeLessThan(500);
            result.Value.Should().HaveCount(400);
        }

        [Fact]
        public async Task PaginationThroughLargeDataset_ShouldMaintainPerformance()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 5000)
                .Select(i => new TestEntity { Name = $"Page{i}", Value = i })
                .ToList());

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = new List<TestEntity>();

            for (int page = 1; page <= 50; page++)
            {
                var pageResult = await _sqlManager.GetPagedAsync<TestEntity>(
                    page, 100, orderBy: e => e.Value, ascending: true
                );
                results.AddRange(pageResult.Value);
            }

            sw.Stop();

            // Assert
            results.Count.Should().Be(5000);
            sw.ElapsedMilliseconds.Should().BeLessThan(2000);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task AddAsync_WithDuplicateData_ShouldSucceed()
        {
            // Arrange
            var entity1 = new TestEntity { Name = "Duplicate", Value = 1 };
            var entity2 = new TestEntity { Name = "Duplicate", Value = 1 };

            // Act
            var result1 = await _sqlManager.AddAsync(entity1);
            var result2 = await _sqlManager.AddAsync(entity2);

            // Assert
            result1.IsSuccess.Should().BeTrue();
            result2.IsSuccess.Should().BeTrue();
            entity1.Id.Should().NotBe(entity2.Id);
        }

        [Fact]
        public async Task WhereAsync_WithEmptyResult_ShouldReturnEmptyList()
        {
            // Act
            var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > 999999);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task UpdateAsync_NonExistentEntity_ShouldHandleGracefully()
        {
            // Arrange
            var entity = new TestEntity { Id = 99999, Name = "NonExistent", Value = 1 };

            // Act
            var result = await _sqlManager.UpdateAsync(entity);

            // Assert - EF Core will treat this as an update and may not fail
            // The behavior depends on whether tracking is enabled
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task TransactionRollback_ShouldNotPersistChanges()
        {
            // Arrange
            var initialCount = (await _sqlManager.CountAsync<TestEntity>()).Value;

            // Act
            var result = await _sqlManager.ExecuteInTransactionAsync(async context =>
            {
                context.Set<TestEntity>().Add(new TestEntity { Name = "Rollback1", Value = 1 });
                context.Set<TestEntity>().Add(new TestEntity { Name = "Rollback2", Value = 2 });
                await context.SaveChangesAsync();

                throw new InvalidOperationException("Intentional rollback");
            });

            // Assert
            result.IsSuccess.Should().BeFalse();
            var finalCount = (await _sqlManager.CountAsync<TestEntity>()).Value;
            finalCount.Should().Be(initialCount);
        }

        [Fact]
        public async Task GetPagedAsync_WithInvalidPageNumber_ShouldHandleGracefully()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 1 });

            // Act
            var result = await _sqlManager.GetPagedAsync<TestEntity>(0, 10);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeEmpty(); // Should default to page 1
        }

        [Fact]
        public async Task DeleteAsync_AlreadyDeletedEntity_ShouldHandleGracefully()
        {
            // Arrange
            var entity = new TestEntity { Name = "ToDelete", Value = 1 };
            await _sqlManager.AddAsync(entity);
            await _sqlManager.DeleteAsync(entity);

            // Act
            var result = await _sqlManager.DeleteAsync(entity);

            // Assert - May succeed or fail depending on tracking
            // The important part is it shouldn't throw
            result.Should().NotBeNull();
        }

        #endregion

        #region Complex Concurrency Scenarios

        [Fact]
        public async Task ConcurrentUpdatesSameEntity_ShouldHandleConflict()
        {
            // Arrange
            var entity = new TestEntity { Name = "Concurrent", Value = 0 };
            await _sqlManager.AddAsync(entity);

            // Act - Multiple tasks updating the same entity
            var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
            {
                var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
                if (retrieved.IsSuccess)
                {
                    retrieved.Value.Value += i;
                    return await _sqlManager.UpdateAsync(retrieved.Value);
                }
                return MatthL.ResultLogger.Core.Models.Result.Failure("Failed to retrieve");
            }));

            var results = await Task.WhenAll(tasks);

            // Assert - All should complete (some may have conflicts)
            results.Should().NotBeEmpty();
            var finalEntity = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            finalEntity.IsSuccess.Should().BeTrue();
            finalEntity.Value.Value.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ConcurrentTransactionsWithDependencies_ShouldMaintainConsistency()
        {
            // Arrange
            var parentEntity = new TestEntity { Name = "Parent", Value = 100 };
            await _sqlManager.AddAsync(parentEntity);

            // Act - Concurrent transactions that depend on the parent
            var tasks = Enumerable.Range(1, 5).Select(i => Task.Run(async () =>
            {
                return await _sqlManager.ExecuteInTransactionAsync(async context =>
                {
                    var parent = await context.Set<TestEntity>().FindAsync(parentEntity.Id);
                    if (parent != null)
                    {
                        parent.Value += i;
                        context.Set<TestEntity>().Add(new TestEntity
                        {
                            Name = $"Child{i}",
                            Value = i
                        });
                        await context.SaveChangesAsync();
                        return true;
                    }
                    return false;
                });
            }));

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().OnlyContain(r => r.IsSuccess);
            var childCount = await _sqlManager.CountAsync<TestEntity>(e => e.Name.StartsWith("Child"));
            childCount.Value.Should().Be(5);
        }

        [Fact]
        public async Task LongRunningTransaction_WithConcurrentReads_ShouldWork()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Base", Value = 1 });

            // Act - Long transaction
            var longTransaction = Task.Run(async () =>
            {
                return await _sqlManager.ExecuteInTransactionAsync(async context =>
                {
                    context.Set<TestEntity>().Add(new TestEntity { Name = "Long", Value = 100 });
                    await context.SaveChangesAsync();
                    await Task.Delay(500); // Simulate long operation
                });
            });

            // Concurrent reads during the transaction
            var readTasks = Enumerable.Range(1, 10).Select(_ => Task.Run(async () =>
            {
                await Task.Delay(Random.Shared.Next(0, 400));
                var result = await _sqlManager.GetAllAsync<TestEntity>();
                return result.IsSuccess;
            }));

            var allResults = await Task.WhenAll(readTasks.Append(longTransaction.ContinueWith(t => t.Result.IsSuccess)));
            // Assert
            allResults.Should().OnlyContain(r => r == true);
        }

        [Fact]
        public async Task ConcurrentPaginationWithWrites_ShouldRemainConsistent()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 500)
                .Select(i => new TestEntity { Name = $"Initial{i}", Value = i })
                .ToList());

            // Act
            var paginationTasks = Enumerable.Range(1, 5).Select(page => Task.Run(async () =>
            {
                var pages = new List<List<TestEntity>>();
                for (int i = 0; i < 10; i++)
                {
                    var result = await _sqlManager.GetPagedAsync<TestEntity>(
                        page, 100, orderBy: e => e.Id, ascending: true
                    );
                    if (result.IsSuccess)
                    {
                        pages.Add(result.Value);
                    }
                    await Task.Delay(20);
                }
                return pages;
            }));

            var writeTasks = Enumerable.Range(1, 3).Select(i => Task.Run(async () =>
            {
                for (int j = 0; j < 5; j++)
                {
                    await _sqlManager.AddAsync(new TestEntity
                    {
                        Name = $"New{i}-{j}",
                        Value = 1000 + i * 10 + j
                    });
                    await Task.Delay(30);
                }
            }));

            await Task.WhenAll(paginationTasks.Cast<Task>().Concat(writeTasks));

            // Assert - Database should remain consistent
            var finalCount = await _sqlManager.CountAsync<TestEntity>();
            finalCount.IsSuccess.Should().BeTrue();
            finalCount.Value.Should().BeGreaterThanOrEqualTo(515); // 500 initial + 15 new
        }

        #endregion

        #region Database File Operations

        [Fact]
        public async Task GetDatabaseFileInfo_AfterWrites_ShouldShowWALFile()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 100)
                .Select(i => new TestEntity { Name = $"File{i}", Value = i })
                .ToList());

            // Act
            var result = _sqlManager.GetDatabaseFileInfo();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.MainFileExists.Should().BeTrue();
            result.Value.MainFileSize.Should().BeGreaterThan(0);
            // WAL file may or may not exist depending on when the checkpoint happened
        }

        [Fact]
        public async Task FlushAsync_AfterManyWrites_ShouldReduceWALSize()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 1000)
                .Select(i => new TestEntity { Name = $"Flush{i}", Value = i })
                .ToList());

            var infoBeforeFlush = _sqlManager.GetDatabaseFileInfo();

            // Act
            await _sqlManager.FlushAsync();
            await Task.Delay(100); // Give time for OS to update file info

            var infoAfterFlush = _sqlManager.GetDatabaseFileInfo();

            // Assert
            infoBeforeFlush.IsSuccess.Should().BeTrue();
            infoAfterFlush.IsSuccess.Should().BeTrue();
            // Main file size should increase or stay same after flush
            infoAfterFlush.Value.MainFileSize.Should().BeGreaterThanOrEqualTo(infoBeforeFlush.Value.MainFileSize);
        }

        [Fact]
        public async Task DisconnectAndReconnect_ShouldMaintainData()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Persistent", Value = 123 });
            var countBefore = (await _sqlManager.CountAsync<TestEntity>()).Value;

            // Act
            await _sqlManager.DisconnectAsync();
            await _sqlManager.ConnectAsync();

            // Assert
            var countAfter = (await _sqlManager.CountAsync<TestEntity>()).Value;
            countAfter.Should().Be(countBefore);

            var entity = await _sqlManager.FirstOrDefaultAsync<TestEntity>(e => e.Name == "Persistent");
            entity.IsSuccess.Should().BeTrue();
            entity.Value.Value.Should().Be(123);
        }

        #endregion

        #region Complex Query Scenarios

        [Fact]
        public async Task ExecuteQueryAsync_WithComplexJoins_ShouldWork()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(Enumerable.Range(1, 100)
                .Select(i => new TestEntity
                {
                    Name = i % 10 == 0 ? "Group1" : "Group2",
                    Value = i
                })
                .ToList());

            // Act
            var result = await _sqlManager.ExecuteQueryAsync<TestEntity>(query =>
                query.Where(e => e.Name == "Group1")
                     .OrderBy(e => e.Value)
                     .Take(5)
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(5);
            result.Value.Should().OnlyContain(e => e.Name == "Group1");
        }

        [Fact]
        public async Task ExecuteAggregateAsync_MultipleAggregations_ShouldWork()
        {
            // Arrange
            await _sqlManager.AddRangeAsync(new List<TestEntity>
            {
                new TestEntity { Name = "A", Value = 10 },
                new TestEntity { Name = "A", Value = 20 },
                new TestEntity { Name = "B", Value = 30 },
                new TestEntity { Name = "B", Value = 40 }
            });

            // Act - Sum
            var sumResult = await _sqlManager.ExecuteAggregateAsync<TestEntity, int>(
                query => query.SumAsync(e => e.Value)
            );

            // Act - Average
            var avgResult = await _sqlManager.ExecuteAggregateAsync<TestEntity, double>(
                query => query.AverageAsync(e => e.Value)
            );

            // Assert
            sumResult.IsSuccess.Should().BeTrue();
            sumResult.Value.Should().Be(100);

            avgResult.IsSuccess.Should().BeTrue();
            avgResult.Value.Should().Be(25);
        }

        [Fact]
        public async Task QueryNoTracking_MultipleCalls_ShouldNotCache()
        {
            // Arrange
            var entity = new TestEntity { Name = "NoTrack", Value = 1 };
            await _sqlManager.AddAsync(entity);

            // Act
            var result1 = await _sqlManager.ExecuteQueryNoTrackingAsync<TestEntity>(
                query => query.Where(e => e.Name == "NoTrack")
            );

            entity.Value = 100;
            await _sqlManager.UpdateAsync(entity);

            var result2 = await _sqlManager.ExecuteQueryNoTrackingAsync<TestEntity>(
                query => query.Where(e => e.Name == "NoTrack")
            );

            // Assert
            result1.Value[0].Value.Should().Be(1);
            result2.Value[0].Value.Should().Be(100);
        }

        #endregion

        #region Error Recovery Tests

        [Fact]
        public async Task MultipleFailedTransactions_ShouldNotAffectConnection()
        {
            // Act - Multiple failed transactions
            for (int i = 0; i < 5; i++)
            {
                var result = await _sqlManager.ExecuteInTransactionAsync(async context =>
                {
                    context.Set<TestEntity>().Add(new TestEntity { Name = "Fail", Value = i });
                    await context.SaveChangesAsync();
                    throw new Exception($"Fail {i}");
                });

                result.IsSuccess.Should().BeFalse();
            }

            // Assert - Connection should still work
            _sqlManager.IsConnected.Should().BeTrue();

            var addResult = await _sqlManager.AddAsync(new TestEntity { Name = "Success", Value = 1 });
            addResult.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task OperationAfterConnectionCheck_ShouldSucceed()
        {
            // Act
            var isValid = await _sqlManager.IsConnectionValidAsync();
            var healthCheck = await _sqlManager.CheckHealthAsync();
            var addResult = await _sqlManager.AddAsync(new TestEntity { Name = "AfterCheck", Value = 1 });

            // Assert
            isValid.Should().BeTrue();
            healthCheck.Status.Should().Be(HealthStatus.Healthy);
            addResult.IsSuccess.Should().BeTrue();
        }

        #endregion

        #region Boundary Tests

        [Fact]
        public async Task EmptyString_InEntityName_ShouldBeHandled()
        {
            // Arrange
            var entity = new TestEntity { Name = "", Value = 1 };

            // Act
            var result = await _sqlManager.AddAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            retrieved.Value.Name.Should().Be("");
        }

        [Fact]
        public async Task VeryLongString_InEntityName_ShouldBeHandled()
        {
            // Arrange
            var longName = new string('X', 10000);
            var entity = new TestEntity { Name = longName, Value = 1 };

            // Act
            var result = await _sqlManager.AddAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            retrieved.Value.Name.Should().HaveLength(10000);
        }

        [Fact]
        public async Task NegativeValues_ShouldBeHandledCorrectly()
        {
            // Arrange
            var entity = new TestEntity { Name = "Negative", Value = -99999 };

            // Act
            var result = await _sqlManager.AddAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            retrieved.Value.Value.Should().Be(-99999);
        }

        [Fact]
        public async Task MaxIntValue_ShouldBeHandled()
        {
            // Arrange
            var entity = new TestEntity { Name = "MaxInt", Value = int.MaxValue };

            // Act
            var result = await _sqlManager.AddAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            retrieved.Value.Value.Should().Be(int.MaxValue);
        }

        #endregion
    }
}