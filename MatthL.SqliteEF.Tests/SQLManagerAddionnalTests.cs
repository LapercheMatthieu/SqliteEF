using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Models;
using MatthL.SqliteEF.Core.Authorizations;
using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using MatthL.SqliteEF.Core.Enums;

namespace MatthL.SqliteEF.Tests
{
    [Collection("SQLiteTests")]
    public class SQLManagerAdditionalTests : IAsyncLifetime
    {
        private SQLManager _sqlManager;
        private SQLManager _inMemoryManager;
        private string _testDbPath;
        private string _testDbName;

        public async Task InitializeAsync()
        {
            // Setup file-based SQLManager
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
            await _sqlManager.ConnectAsync();

            // Setup in-memory SQLManager
            _inMemoryManager = new SQLManager(() => new TestDbContext());
            await _inMemoryManager.Create();
            await _inMemoryManager.ConnectAsync();
        }

        public async Task DisposeAsync()
        {
            await _sqlManager.CloseConnectionsAsync();
            await _sqlManager.DeleteCurrentDatabase();

            await _inMemoryManager.CloseConnectionsAsync();

            if (Directory.Exists(_testDbPath))
            {
                Directory.Delete(_testDbPath, true);
            }
        }

        [Fact]
        public void Constructor_InMemory_ShouldInitializeCorrectly()
        {
            // Act
            var manager = new SQLManager(() => new TestDbContext());

            // Assert
            manager.Should().NotBeNull();
            manager.IsConnected.Should().BeFalse();
            manager.CurrentState.Should().Be(ConnectionState.Disconnected);
        }

        [Fact]
        public async Task GetFileSize_ShouldReturnCorrectSize()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test", Value = 123 };
            await _sqlManager.AddAsync(entity);
            await _sqlManager.FlushAsync();

            // Act
            var fileSize = _sqlManager.GetFileSize;

            // Assert
            fileSize.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GetService_ShouldReturnValidService()
        {
            // Act
            var result = _sqlManager.GetService<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
        }

        [Fact]
        public void GetService_ShouldReturnSameInstanceForSameType()
        {
            // Act
            var result1 = _sqlManager.GetService<TestEntity>();
            var result2 = _sqlManager.GetService<TestEntity>();

            // Assert
            result1.Value.Should().BeSameAs(result2.Value);
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
            var updated = await _sqlManager.GetAllAsync<TestEntity>();
            updated.Value.Should().Contain(e => e.Value == 10);
            updated.Value.Should().Contain(e => e.Value == 20);
        }

        [Fact]
        public async Task DeleteRangeAsync_ShouldDeleteMultipleEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "ToDelete1", Value = 1 },
                new TestEntity { Name = "ToDelete2", Value = 2 },
                new TestEntity { Name = "ToKeep", Value = 3 }
            };
            await _sqlManager.AddRangeAsync(entities);

            // Act
            var toDelete = entities.Take(2).ToList();
            var result = await _sqlManager.DeleteRangeAsync(toDelete);

            // Assert
            result.IsSuccess.Should().BeTrue();
            var remaining = await _sqlManager.GetAllAsync<TestEntity>();
            remaining.Value.Should().HaveCount(1);
            remaining.Value[0].Name.Should().Be("ToKeep");
        }

        [Fact]
        public async Task DeleteAllAsync_ShouldRemoveAllEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity1", Value = 1 },
                new TestEntity { Name = "Entity2", Value = 2 },
                new TestEntity { Name = "Entity3", Value = 3 }
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
        public async Task AddOrUpdateAsync_ShouldAddNewEntity()
        {
            // Arrange
            var entity = new TestEntity { Name = "New", Value = 42 };

            // Act
            var result = await _sqlManager.AddOrUpdateAsync(entity);

            // Assert
            result.IsSuccess.Should().BeTrue();
            entity.Id.Should().BeGreaterThan(0);
            var retrieved = await _sqlManager.GetByIdAsync<TestEntity>(entity.Id);
            retrieved.Value.Name.Should().Be("New");
        }

        [Fact]
        public async Task AddOrUpdateAsync_ShouldUpdateExistingEntity()
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

        [Fact]
        public async Task AnyExistAsync_ShouldReturnTrue_WhenEntitiesExist()
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
        public async Task AnyExistAsync_ShouldReturnFalse_WhenNoEntities()
        {
            // Act
            var result = await _sqlManager.AnyExistAsync<TestEntity>();

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeFalse();
        }

        [Fact]
        public void IsSavable_ShouldValidateEntity()
        {
            // Arrange
            var validEntity = new TestEntity { Name = "Valid", Value = 1 };
            var invalidEntity = new TestEntity { Name = null, Value = 1 }; // Assuming Name is required

            // Act
            var validResult = _sqlManager.IsSavable(validEntity);
            var invalidResult = _sqlManager.IsSavable(invalidEntity);

            // Assert
            validResult.IsSuccess.Should().BeTrue();
            validResult.Value.Should().BeTrue();
            // Note: This depends on your validation logic
        }

        [Fact]
        public void SetPaths_WithParameters_ShouldUpdatePaths()
        {
            // Arrange
            var newPath = Path.Combine(Path.GetTempPath(), "NewPath");
            var newName = "NewDatabase";

            // Act
            _sqlManager.SetPaths(newPath, newName);

            // Assert
            _sqlManager.GetFolderPath.Should().Be(newPath);
            _sqlManager.GetFileName.Should().Be(newName);
            _sqlManager.GetFullPath.Should().Be(Path.Combine(newPath, newName + ".db"));
        }

        [Fact]
        public void SetPaths_WithoutParameters_ShouldKeepExistingPaths()
        {
            // Arrange
            var originalPath = _sqlManager.GetFolderPath;
            var originalName = _sqlManager.GetFileName;

            // Act
            _sqlManager.SetPaths();

            // Assert
            _sqlManager.GetFolderPath.Should().Be(originalPath);
            _sqlManager.GetFileName.Should().Be(originalName);
        }

        [Fact]
        public async Task DisconnectAsync_ShouldDisconnectProperly()
        {
            // Arrange
            await _sqlManager.ConnectAsync();

            // Act
            var result = await _sqlManager.DisconnectAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            _sqlManager.IsConnected.Should().BeFalse();
            _sqlManager.CurrentState.Should().Be(ConnectionState.Disconnected);
        }

        [Fact]
        public async Task IsConnectionValidAsync_ShouldReturnTrue_WhenConnected()
        {
            // Arrange
            await _sqlManager.ConnectAsync();

            // Act
            var isValid = await _sqlManager.IsConnectionValidAsync();

            // Assert
            isValid.Should().BeTrue();
        }

        [Fact]
        public async Task IsConnectionValidAsync_ShouldReturnFalse_WhenDisconnected()
        {
            await _sqlManager.DisconnectAsync();
            // Act
            var isValid = await _sqlManager.IsConnectionValidAsync();

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task CheckHealthAsync_ShouldReturnHealthStatus()
        {
            // Arrange
            await _sqlManager.ConnectAsync();

            // Act
            var health = await _sqlManager.CheckHealthAsync();

            // Assert
            health.Should().NotBeNull();
            health.Status.Should().Be(HealthStatus.Healthy);
            health.Details.Should().ContainKey("pingMs");
        }

        [Fact]
        public async Task FlushAsync_ShouldFlushData()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            await _sqlManager.AddAsync(new TestEntity { Name = "Test", Value = 1 });

            // Act
            var result = await _sqlManager.FlushAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
          //  result.Message.Should().Contain("Flush completed");
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_WithResult_ShouldReturnValue()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var expectedValue = 42;

            // Act
            var result = await _sqlManager.ExecuteInTransactionAsync(async () =>
            {
                var entity = new TestEntity { Name = "Transaction", Value = expectedValue };
                await _sqlManager.AddAsync(entity);
                return expectedValue;
            });

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(expectedValue);
        }

        [Fact]
        public async Task CloseConnectionsAsync_ShouldCloseAndClearPools()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var service = _sqlManager.GetService<TestEntity>();

            // Act
            var result = await _sqlManager.CloseConnectionsAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            _sqlManager.IsConnected.Should().BeFalse();

            // Services should be cleared, so getting service again should return new instance
            var newService = _sqlManager.GetService<TestEntity>();
            newService.Value.Should().NotBeSameAs(service.Value);
        }

        [Fact]
        public async Task RefreshContextAsync_ShouldRecreateContext()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            await _sqlManager.AddAsync(new TestEntity { Name = "Before", Value = 1 });
            var stateBeforeRefresh = _sqlManager.CurrentState;

            // Act
            var result = await _sqlManager.RefreshContextAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
           // result.Message.Should().Contain("Context refreshed");
            _sqlManager.CurrentState.Should().Be(ConnectionState.Disconnected);

            // Should be able to reconnect after refresh
            await _sqlManager.ConnectAsync();
            _sqlManager.IsConnected.Should().BeTrue();
        }

        [Fact]
        public async Task RefreshContextAsync_ShouldClearServices()
        {
            // Arrange
            await _sqlManager.ConnectAsync();
            var serviceBefore = _sqlManager.GetService<TestEntity>();

            // Act
            await _sqlManager.RefreshContextAsync();
            await _sqlManager.ConnectAsync();
            var serviceAfter = _sqlManager.GetService<TestEntity>();

            // Assert
            serviceAfter.Value.Should().NotBeSameAs(serviceBefore.Value);
        }
    }

}