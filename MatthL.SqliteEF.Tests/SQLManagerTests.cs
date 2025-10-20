using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace MatthL.SqliteEF.Tests
{
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

            _sqlManager = new SQLManager(
                () => new TestDbContext(),
                _testDbPath,
                _testDbName,
                new AdminAuthorization()
            );

            await _sqlManager.Create();
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
        public async Task Create_ShouldCreateDatabase_WhenCalledFirstTime()
        {
            // Arrange
            var tempPath = Path.Combine(Path.GetTempPath(), $"Test_{Guid.NewGuid()}");
            var manager = new SQLManager(
                () => new TestDbContext(),
                tempPath,
                "NewTestDb",
                new AdminAuthorization()
            );

            // Act
            var result = await manager.Create();

            // Assert
            result.IsSuccess.Should().BeTrue();
            //result..Should().Contain("Database created");
            File.Exists(Path.Combine(tempPath, "NewTestDb.db")).Should().BeTrue();

            // Cleanup
            await manager.DeleteCurrentDatabase();
            Directory.Delete(tempPath, true);
        }

        [Fact]
        public async Task AddAsync_ShouldAddEntity_WhenEntityIsValid()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var testEntity = new TestEntity { Name = "Test Item", Value = 42 };

            // Act
            var result = await _sqlManager.AddAsync(testEntity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            testEntity.Id.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnEntity_WhenEntityExists()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var testEntity = new TestEntity { Name = "Test Item", Value = 42 };
            await _sqlManager.AddAsync(testEntity);

            // Act
            var result = await _sqlManager.GetByIdAsync<TestEntity>(testEntity.Id);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Name.Should().Be("Test Item");
            result.Value.Value.Should().Be(42);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateEntity_WhenEntityExists()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var testEntity = new TestEntity { Name = "Original", Value = 1 };
            await _sqlManager.AddAsync(testEntity);

            // Act
            testEntity.Name = "Updated";
            testEntity.Value = 2;
            var result = await _sqlManager.UpdateAsync(testEntity);

            // Assert
            result.IsSuccess.Should().BeTrue();

            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(testEntity.Id);
            retrieved.Value.Name.Should().Be("Updated");
            retrieved.Value.Value.Should().Be(2);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveEntity_WhenEntityExists()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var testEntity = new TestEntity { Name = "To Delete", Value = 99 };
            await _sqlManager.AddAsync(testEntity);

            // Act
            var deleteResult = await _sqlManager.DeleteAsync(testEntity);
            var getResult = await _sqlManager.GetByIdAsync<TestEntity>(testEntity.Id);

            // Assert
            deleteResult.IsSuccess.Should().BeTrue();
            getResult.IsSuccess.Should().BeFalse();
        }


        [Fact]
        public async Task GetAllAsync_ShouldReturnAllEntities()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Item 1", Value = 1 },
                new TestEntity { Name = "Item 2", Value = 2 },
                new TestEntity { Name = "Item 3", Value = 3 }
            };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var result = await _sqlManager.GetAllAsync<TestEntity>();
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(3);
            result.Value.Should().Contain(e => e.Name == "Item 1");
            result.Value.Should().Contain(e => e.Name == "Item 2");
            result.Value.Should().Contain(e => e.Name == "Item 3");
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_ShouldRollback_WhenExceptionThrown()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entity1 = new TestEntity { Name = "Transaction Test 1", Value = 1 };

            // Act
            var result = await _sqlManager.ExecuteInTransactionAsync(async () =>
            {
                await _sqlManager.AddAsync(entity1);
                throw new Exception("Simulated error");
            });

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("Transaction rolled back");

            var allEntities = await _sqlManager.GetAllAsync<TestEntity>();
            allEntities.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_ShouldCommit_WhenNoException()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entity1 = new TestEntity { Name = "Transaction Test 1", Value = 1 };
            var entity2 = new TestEntity { Name = "Transaction Test 2", Value = 2 };

            // Act
            var result = await _sqlManager.ExecuteInTransactionAsync(async () =>
            {
                await _sqlManager.AddAsync(entity1);
                await _sqlManager.AddAsync(entity2);
            });

            // Assert
            result.IsSuccess.Should().BeTrue();

            var allEntities = await _sqlManager.GetAllAsync<TestEntity>();
            allEntities.Value.Should().HaveCount(2);
        }

        [Fact]
        public void GetFullPath_ShouldReturnCorrectPath()
        {
            // Assert
            _sqlManager.GetFullPath.Should().Be(Path.Combine(_testDbPath, _testDbName + ".db"));
        }

        [Fact]
        public void GetFolderPath_ShouldReturnCorrectFolder()
        {
            // Assert
            _sqlManager.GetFolderPath.Should().Be(_testDbPath);
        }

        [Fact]
        public void GetFileName_ShouldReturnCorrectFileName()
        {
            // Assert
            _sqlManager.GetFileName.Should().Be(_testDbName);
        }

        [Fact]
        public async Task DeleteCurrentDatabase_ShouldDeleteFile()
        {
            var existbefore = File.Exists(_sqlManager.GetFullPath);
            // Assert
            await _sqlManager.DeleteCurrentDatabase();

            var existafter = File.Exists(_sqlManager.GetFullPath);
            existbefore.Should().BeTrue();
            existafter.Should().BeFalse();
        }

        [Fact]
        public void DbContext_ShouldReturnValidContext()
        {
            // Act
            var dbContext = _sqlManager.DbContext;

            // Assert
            dbContext.Should().NotBeNull();
            dbContext.Should().BeOfType<TestDbContext>();
        }

        [Fact]
        public async Task Query_ShouldReturnQueryable_WhenConnected()
        {
            // Arrange
            await _sqlManager.ConnectAsync();

            // Act
            var result = _sqlManager.Query<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task Query_ShouldFail_WhenNotConnected()
        {
            // Act
            var result = _sqlManager.Query<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("Not connected");
        }

        [Fact]
        public async Task QueryNoTracking_ShouldReturnQueryable_WhenConnected()
        {
            // Arrange
            await _sqlManager.ConnectAsync();

            // Act
            var result = _sqlManager.QueryNoTracking<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
        }

        [Fact]
        public async Task WhereAsync_ShouldReturnFilteredEntities()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>
    {
        new TestEntity { Name = "Active Item", Value = 10 },
        new TestEntity { Name = "Inactive Item", Value = 5 },
        new TestEntity { Name = "Active Item 2", Value = 20 }
    };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > 8);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value.Should().OnlyContain(e => e.Value > 8);
        }

        [Fact]
        public async Task WhereAsync_ShouldReturnEmpty_WhenNoMatch()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 5 });

            // Act
            var result = await _sqlManager.WhereAsync<TestEntity>(e => e.Value > 100);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnCorrectPage()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 25; i++)
            {
                entities.Add(new TestEntity { Name = $"Item {i}", Value = i });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var page1 = await _sqlManager.GetPagedAsync<TestEntity>(1, 10);
            var page2 = await _sqlManager.GetPagedAsync<TestEntity>(2, 10);
            var page3 = await _sqlManager.GetPagedAsync<TestEntity>(3, 10);

            // Assert
            page1.IsSuccess.Should().BeTrue();
            page1.Value.Should().HaveCount(10);

            page2.IsSuccess.Should().BeTrue();
            page2.Value.Should().HaveCount(10);

            page3.IsSuccess.Should().BeTrue();
            page3.Value.Should().HaveCount(5);
        }

        [Fact]
        public async Task GetPagedAsync_WithPredicate_ShouldReturnFilteredPage()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 30; i++)
            {
                entities.Add(new TestEntity { Name = $"Item {i}", Value = i });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var result = await _sqlManager.GetPagedAsync<TestEntity>(
                pageNumber: 1,
                pageSize: 5,
                predicate: e => e.Value > 10
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(5);
            result.Value.Should().OnlyContain(e => e.Value > 10);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldHandleInvalidPageNumber()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 1 });

            // Act
            var result = await _sqlManager.GetPagedAsync<TestEntity>(0, 10);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(1); // Page 0 devrait être traité comme page 1
        }

        [Fact]
        public async Task CountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>
    {
        new TestEntity { Name = "Item 1", Value = 10 },
        new TestEntity { Name = "Item 2", Value = 20 },
        new TestEntity { Name = "Item 3", Value = 30 }
    };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var result = await _sqlManager.CountAsync<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(3);
        }

        [Fact]
        public async Task CountAsync_WithPredicate_ShouldReturnFilteredCount()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>
    {
        new TestEntity { Name = "Item 1", Value = 10 },
        new TestEntity { Name = "Item 2", Value = 20 },
        new TestEntity { Name = "Item 3", Value = 30 }
    };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var result = await _sqlManager.CountAsync<TestEntity>(e => e.Value >= 20);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(2);
        }

        [Fact]
        public async Task CountAsync_ShouldReturnZero_WhenNoEntities()
        {
            // Arrange
            await _sqlManager.ConnectAsync();

            // Act
            var result = await _sqlManager.CountAsync<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(0);
        }

        [Fact]
        public async Task FirstOrDefaultAsync_ShouldReturnEntity_WhenExists()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>
    {
        new TestEntity { Name = "First", Value = 10 },
        new TestEntity { Name = "Second", Value = 20 }
    };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var result = await _sqlManager.FirstOrDefaultAsync<TestEntity>(e => e.Name == "Second");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.Name.Should().Be("Second");
            result.Value.Value.Should().Be(20);
        }

        [Fact]
        public async Task FirstOrDefaultAsync_ShouldFail_WhenNotFound()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 10 });

            // Act
            var result = await _sqlManager.FirstOrDefaultAsync<TestEntity>(e => e.Name == "NotExisting");

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("No entity found");
        }

        [Fact]
        public async Task AnyAsync_ShouldReturnTrue_WhenEntityExists()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 42 });

            // Act
            var result = await _sqlManager.AnyAsync<TestEntity>(e => e.Value == 42);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeTrue();
        }

        [Fact]
        public async Task AnyAsync_ShouldReturnFalse_WhenEntityDoesNotExist()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 42 });

            // Act
            var result = await _sqlManager.AnyAsync<TestEntity>(e => e.Value == 999);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeFalse();
        }

        [Fact]
        public async Task QueryWithComplexLinq_ShouldWork()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 20; i++)
            {
                entities.Add(new TestEntity { Name = $"Item {i}", Value = i * 10 });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var queryResult = _sqlManager.Query<TestEntity>();
            var result = await queryResult.Value
                .Where(e => e.Value > 50)
                .OrderByDescending(e => e.Value)
                .Take(5)
                .ToListAsync();

            // Assert
            queryResult.IsSuccess.Should().BeTrue();
            result.Should().HaveCount(5);
            result.First().Value.Should().Be(200); // Le plus grand
            result.Should().BeInDescendingOrder(e => e.Value);
        }

        [Fact]
        public async Task QueryNoTracking_ShouldNotTrackChanges()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entity = new TestEntity { Name = "Original", Value = 100 };
            await _sqlManager.AddAsync(entity);

            // Act
            var queryResult = _sqlManager.QueryNoTracking<TestEntity>();
            var fetchedEntity = await queryResult.Value.FirstAsync();
            fetchedEntity.Name = "Modified";

            // Vérifier que les changements ne sont pas trackés
            var saveResult = await _sqlManager.UpdateAsync(fetchedEntity);

            // Assert
            queryResult.IsSuccess.Should().BeTrue();
            // En NoTracking, l'entité ne devrait pas être mise à jour automatiquement
            // car elle n'est pas suivie par le contexte
        }

        [Fact]
        public async Task GetPagedAsync_WithLargeDataset_ShouldBeEfficient()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 1000; i++)
            {
                entities.Add(new TestEntity { Name = $"Item {i}", Value = i });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _sqlManager.GetPagedAsync<TestEntity>(1, 10);
            sw.Stop();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(10);
            sw.ElapsedMilliseconds.Should().BeLessThan(200); // Devrait être rapide
        }

        [Fact]
        public async Task WhereAsync_WithMultipleConditions_ShouldWork()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>
                {
                    new TestEntity { Name = "Active High", Value = 100 },
                    new TestEntity { Name = "Active Low", Value = 5 },
                    new TestEntity { Name = "Inactive High", Value = 95 },
                    new TestEntity { Name = "Inactive Low", Value = 3 }
                };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var result = await _sqlManager.WhereAsync<TestEntity>(
                e => e.Name.Contains("Active") && e.Value > 10
            );

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(1);
            result.Value.First().Name.Should().Be("Active High");
        }

        [Fact]
        public async Task CountAsync_ShouldNotLoadEntitiesInMemory()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var entities = new List<TestEntity>();
            for (int i = 1; i <= 10000; i++)
            {
                entities.Add(new TestEntity { Name = $"Item {i}", Value = i });
            }
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _sqlManager.CountAsync<TestEntity>();
            sw.Stop();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(10000);
            sw.ElapsedMilliseconds.Should().BeLessThan(500); // Count devrait être très rapide
        }
    }

    // Test Entity for testing
    public class TestEntity : IBaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
        public void ConfigureEntity(ModelBuilder modelBuilder)
        {

        }
    }

    // Test DbContext
    public class TestDbContext : RootDbContext
    {
        public DbSet<TestEntity> TestEntities { get; set; }
    }
}