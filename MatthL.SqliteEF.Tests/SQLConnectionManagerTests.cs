using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MatthL.SqliteEF.Core.Enums;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Core.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace MatthL.SqliteEF.Tests
{
    public class SQLConnectionManagerTests
    {
        private SQLConnectionManager _connectionManager;
        private TestDbContext _dbContext;

        public SQLConnectionManagerTests()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;

            _dbContext = new TestDbContext();
            _dbContext.Database.OpenConnection();
            _dbContext.Database.EnsureCreated();

            _connectionManager = new SQLConnectionManager(_dbContext);
        }

        [Fact]
        public async Task ConnectAsync_ShouldChangeStateToConnected_WhenSuccessful()
        {
            // Act
            var result = await _connectionManager.ConnectAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            _connectionManager.IsConnected.Should().BeTrue();
            _connectionManager.CurrentState.Should().Be(ConnectionState.Connected);
            _connectionManager.LastConnectionTime.Should().NotBeNull();
        }

        [Fact]
        public async Task ConnectAsync_ShouldReturnSuccess_WhenAlreadyConnected()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _connectionManager.ConnectAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
          //  result.Message.Should().Contain("Already connected");
        }

        [Fact]
        public async Task DisconnectAsync_ShouldChangeStateToDisconnected()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _connectionManager.DisconnectAsync();

            // Assert
            result.IsSuccess.Should().BeTrue();
            _connectionManager.IsConnected.Should().BeFalse();
            _connectionManager.CurrentState.Should().Be(ConnectionState.Disconnected);
        }

        [Fact]
        public async Task IsConnectionValidAsync_ShouldReturnTrue_WhenConnected()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var isValid = await _connectionManager.IsConnectionValidAsync();

            // Assert
            isValid.Should().BeTrue();
            _connectionManager.LastActivityTime.Should().NotBeNull();
        }

        [Fact]
        public async Task IsConnectionValidAsync_ShouldReturnFalse_WhenNotConnected()
        {
            // Act
            var isValid = await _connectionManager.IsConnectionValidAsync();

            // Assert
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task ChangeStateAsync_ShouldTriggerEvent_WhenStateChanges()
        {
            // Arrange
            ConnectionState? eventState = null;
            _connectionManager.ConnectionStateChanged += (sender, state) => eventState = state;

            // Act
            await _connectionManager.ChangeStateAsync(ConnectionState.Connecting);

            // Assert
            eventState.Should().Be(ConnectionState.Connecting);
        }

        [Fact]
        public void UpdateActivity_ShouldUpdateLastActivityTime()
        {
            // Arrange
            var initialTime = _connectionManager.LastActivityTime;

            // Act
            System.Threading.Thread.Sleep(10); // Small delay to ensure time difference
            _connectionManager.UpdateActivity();

            // Assert
            _connectionManager.LastActivityTime.Should().BeAfter(initialTime ?? DateTime.MinValue);
        }

        [Fact]
        public async Task ExecuteInTransactionAsync_ShouldFailWhenNotConnected()
        {
            // Act
            var result = await _connectionManager.ExecuteInTransactionAsync(async () =>
            {
                await Task.CompletedTask;
            });

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("Not connected");
        }

        [Fact]
        public async Task UpdateContext_ShouldResetState()
        {
            // Arrange
            var newContext = new TestDbContext();

            // Act
            await _connectionManager.UpdateContext(newContext);

            // Assert
            _connectionManager.CurrentState.Should().Be(ConnectionState.Disconnected);
            _connectionManager.LastActivityTime.Should().BeNull();
            _connectionManager.LastConnectionTime.Should().BeNull();
        }
    }
}