using Xunit;
using FluentAssertions;
using Moq;
using MatthL.SqliteEF.Core.Managers.Delegates;
using MatthL.SqliteEF.Core.Enums;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace MatthL.SqliteEF.Tests
{
    public class SQLHealthCheckerTests
    {
        private SQLHealthChecker _healthChecker;
        private SQLConnectionManager _connectionManager;
        private TestDbContext _dbContext;

        public SQLHealthCheckerTests()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;

            _dbContext = new TestDbContext();
            _dbContext.Database.OpenConnection();
            _dbContext.Database.EnsureCreated();

            _connectionManager = new SQLConnectionManager(_dbContext);
            _healthChecker = new SQLHealthChecker(_connectionManager);
        }

        [Fact]
        public async Task CheckHealthAsync_ShouldReturnUnhealthy_WhenNotConnected()
        {
            // Act
            var result = await _healthChecker.CheckHealthAsync();

            // Assert
            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Description.Should().Contain("disconnected");
            result.Details.Should().ContainKey("state");
            result.Details["isConnected"].Should().Be(false);
        }

        [Fact]
        public async Task CheckHealthAsync_ShouldReturnHealthy_WhenConnected()
        {
            // Arrange
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _healthChecker.CheckHealthAsync(isInMemory: true);

            // Assert
            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Contain("operational");
            result.Details.Should().ContainKey("pingMs");
            result.Details["isConnected"].Should().Be(true);
        }

        [Fact]
        public async Task CheckHealthAsync_ShouldIncludeDatabaseSize_WhenNotInMemory()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            await _connectionManager.ConnectAsync();

            // Act
            var result = await _healthChecker.CheckHealthAsync(tempFile, false);

            // Assert
            result.Details.Should().ContainKey("dbSizeMB");

            // Cleanup
            File.Delete(tempFile);
        }

        [Fact]
        public async Task CheckHealthAsync_ShouldUpdateLastActivity_WhenSuccessful()
        {
            // Arrange
            await _connectionManager.ConnectAsync();
            var initialActivity = _connectionManager.LastActivityTime;

            // Act
            await Task.Delay(10);
            await _healthChecker.CheckHealthAsync(isInMemory: true);

            // Assert
            _connectionManager.LastActivityTime.Should().BeAfter(initialActivity.Value);
        }
    }
}