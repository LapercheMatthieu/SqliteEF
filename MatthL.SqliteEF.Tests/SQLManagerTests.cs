using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MatthL.SqliteEF.Core.Authorizations;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers.Delegates;
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