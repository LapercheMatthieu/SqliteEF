using FluentAssertions;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Managers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MatthL.SqliteEF.Tests
{
    [Collection("SQLiteIntegrationTests")]
    public class SQLManagerIntegrationTests : IAsyncLifetime
    {
        private SQLManager _sqlManager;
        private string _testDbPath;
        private string _testDbName;

        public async Task InitializeAsync()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), "SQLiteIntegrationTests");
            _testDbName = $"IntegrationTestDb_{Guid.NewGuid()}";

            Directory.CreateDirectory(_testDbPath);

            _sqlManager = new SQLManager(
                () => new TestDbContext(),
                _testDbPath,
                _testDbName,
                new AdminAuthorization()
            );

            await _sqlManager.Create();
            await _sqlManager.ConnectAsync();
        }

        public async Task DisposeAsync()
        {
            await _sqlManager.CloseConnectionsAsync();
            await _sqlManager.DeleteCurrentDatabase();

            if (Directory.Exists(_testDbPath))
            {
                Directory.Delete(_testDbPath, true);
            }
        }

        [Fact]
        public async Task ConcurrentReadsAndWrites_ShouldWork()
        {
            // Arrange - Ajouter des données initiales
            var initialEntities = new List<TestEntity>();
            for (int i = 1; i <= 100; i++)
            {
                initialEntities.Add(new TestEntity { Name = $"Initial {i}", Value = i });
            }
            await _sqlManager.AddRangeAsync(initialEntities);

            // Act - Exécuter des lectures et écritures en parallèle
            var readTasks = new List<Task>();
            var writeTasks = new List<Task>();

            // 5 tâches de lecture
            for (int i = 0; i < 5; i++)
            {
                int taskNum = i;
                readTasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > taskNum * 10);
                        result.IsSuccess.Should().BeTrue();
                        await Task.Delay(5);
                    }
                }));
            }

            // 2 tâches d'écriture
            for (int i = 0; i < 2; i++)
            {
                int taskNum = i;
                writeTasks.Add(Task.Run(async () =>
                {
                    for (int j = 0; j < 5; j++)
                    {
                        var entity = new TestEntity
                        {
                            Name = $"Concurrent Write {taskNum}-{j}",
                            Value = 1000 + (taskNum * 100) + j
                        };
                        var result = await _sqlManager.AddAsync(entity);
                        result.IsSuccess.Should().BeTrue();
                        await Task.Delay(10);
                    }
                }));
            }

            // Assert - Toutes les tâches devraient se terminer sans erreur
            await Task.WhenAll(readTasks.Concat(writeTasks));

            var finalCount = await _sqlManager.CountAsync<TestEntity>();
            finalCount.Value.Should().Be(110); // 100 initiales + 10 ajoutées
        }

        [Fact]
        public async Task PaginationThroughLargeDataset_ShouldBeEfficient()
        {
            // Arrange - Créer un large dataset
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 5000; i++)
            {
                entities.Add(new TestEntity { Name = $"Item {i}", Value = i });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act - Parcourir toutes les pages
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int pageSize = 100;
            int totalProcessed = 0;

            for (int page = 1; page <= 50; page++)
            {
                var pageResult = await _sqlManager.GetPagedAsync<TestEntity>(page, pageSize);
                pageResult.IsSuccess.Should().BeTrue();
                totalProcessed += pageResult.Value.Count;
            }

            sw.Stop();

            // Assert
            totalProcessed.Should().Be(5000);
            sw.ElapsedMilliseconds.Should().BeLessThan(2000); // Devrait être rapide même avec 5000 entités
        }

        [Fact]
        public async Task ComplexQueryWithDbContext_ShouldWork()
        {
            // Arrange
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 100; i++)
            {
                entities.Add(new TestEntity
                {
                    Name = i % 2 == 0 ? "Even" : "Odd",
                    Value = i
                });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act - Utiliser DbContext directement pour une requête complexe
            var result = await _sqlManager.DbContext.Set<TestEntity>()
                .Where(e => e.Name == "Even")
                .GroupBy(e => e.Name)
                .Select(g => new
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Sum = g.Sum(e => e.Value),
                    Average = g.Average(e => e.Value)
                })
                .FirstOrDefaultAsync();

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Even");
            result.Count.Should().Be(50);
            result.Sum.Should().Be(2550); // Somme des nombres pairs de 2 à 100
        }

        [Fact]
        public async Task MixedOperations_QueryAndCrud_ShouldWorkTogether()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Initial", Value = 1 });

            // Act & Assert - Mélanger différentes opérations

            // 1. Query
            var queryResult = _sqlManager.Query<TestEntity>();
            queryResult.IsSuccess.Should().BeTrue();

            // 2. Count
            var count1 = await _sqlManager.CountAsync<TestEntity>();
            count1.Value.Should().Be(1);

            // 3. Add
            await _sqlManager.AddAsync(new TestEntity { Name = "Second", Value = 2 });

            // 4. Where
            var whereResult = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > 0);
            whereResult.Value.Should().HaveCount(2);

            // 5. Update
            var entity = whereResult.Value.First();
            entity.Name = "Updated";
            await _sqlManager.UpdateAsync(entity);

            // 6. FirstOrDefault
            var updated = await _sqlManager.FirstOrDefaultAsync<TestEntity>(e => e.Name == "Updated");
            updated.IsSuccess.Should().BeTrue();

            // 7. Delete
            await _sqlManager.DeleteAsync(updated.Value);

            // 8. Final count
            var finalCount = await _sqlManager.CountAsync<TestEntity>();
            finalCount.Value.Should().Be(1);
        }

        [Fact]
        public async Task ConcurrentPagination_MultipleTasks_ShouldWork()
        {
            // Arrange
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 1000; i++)
            {
                entities.Add(new TestEntity { Name = $"Item {i}", Value = i });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act - Plusieurs tâches paginant en même temps
            var tasks = new List<Task<int>>();

            for (int taskNum = 0; taskNum < 5; taskNum++)
            {
                int startPage = taskNum * 2 + 1;
                tasks.Add(Task.Run(async () =>
                {
                    int processedCount = 0;
                    for (int page = startPage; page < startPage + 10; page++)
                    {
                        var result = await _sqlManager.GetPagedAsync<TestEntity>(page, 10);
                        if (result.IsSuccess)
                        {
                            processedCount += result.Value.Count;
                        }
                    }
                    return processedCount;
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Sum().Should().Be(500); // 5 tâches * 10 pages * 10 items
        }

        [Fact]
        public async Task QueryNoTracking_ShouldBeFasterThanRegularQuery()
        {
            // Arrange
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 10000; i++)
            {
                entities.Add(new TestEntity { Name = $"Item {i}", Value = i });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act - Query régulière
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var queryResult = _sqlManager.Query<TestEntity>();
            var tracked = await queryResult.Value.Take(1000).ToListAsync();
            sw1.Stop();

            // Query NoTracking
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var queryNoTrackResult = _sqlManager.QueryNoTracking<TestEntity>();
            var noTracked = await queryNoTrackResult.Value.Take(1000).ToListAsync();
            sw2.Stop();

            // Assert
            tracked.Should().HaveCount(1000);
            noTracked.Should().HaveCount(1000);

            // NoTracking devrait être plus rapide (ou au moins pas plus lent)
            sw2.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(sw1.ElapsedMilliseconds + 50); // Marge de 50ms
        }

        [Fact]
        public async Task HealthCheck_WhilePerformingOperations_ShouldStillWork()
        {
            try
            {
                // Arrange
                var entities = new List<TestEntity>();
                for (int i = 1; i <= 100; i++)
                {
                    entities.Add(new TestEntity { Name = $"Item {i}", Value = i });
                }
                await _sqlManager.AddRangeAsync(entities);

                // Act - Faire des opérations en parallèle avec des health checks
                var tasks = new List<Task>();

                // Tâche de health check répétée
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        var health = await _sqlManager.CheckHealthAsync();
                        health.Status.Should().NotBe(MatthL.SqliteEF.Core.Enums.HealthStatus.Unhealthy);
                        await Task.Delay(50);
                    }
                }));

                // Tâches de query
                for (int i = 0; i < 3; i++)
                {
                    int taskNum = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 5; j++)
                        {
                            var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > taskNum * 20);
                            result.IsSuccess.Should().BeTrue();
                            await Task.Delay(30);
                        }
                    }));
                }

                // Assert
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.Message);
            }
        }

        [Fact]
        public async Task TransactionWithQueries_ShouldMaintainIsolation()
        {
            // Arrange
            await _sqlManager.AddAsync(new TestEntity { Name = "Initial", Value = 100 });

            // Act - Transaction qui fait des queries et des modifications
            var result = await _sqlManager.ExecuteInTransactionAsync(async () =>
            {
                // Query dans la transaction
                var entities = await _sqlManager.WhereAsync<TestEntity>(e => e.Value == 100);
                entities.IsSuccess.Should().BeTrue();
                entities.Value.Should().HaveCount(1);

                // Modification
                var entity = entities.Value.First();
                entity.Value = 200;
                await _sqlManager.UpdateAsync(entity);

                // Vérification dans la transaction
                var updated = await _sqlManager.FirstOrDefaultAsync<TestEntity>(e => e.Value == 200);
                updated.IsSuccess.Should().BeTrue();
            });

            // Assert
            result.IsSuccess.Should().BeTrue();
            var final = await _sqlManager.FirstOrDefaultAsync<TestEntity>(e => e.Value == 200);
            final.IsSuccess.Should().BeTrue();
        }
    }
}
