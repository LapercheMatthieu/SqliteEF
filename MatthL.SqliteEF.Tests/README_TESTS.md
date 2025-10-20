# SQLManager Test Suite

## Overview
This test suite provides comprehensive testing for the SQLManager component with a strong focus on concurrency, performance, and real-world scenarios.

## Test Files

### 1. SQLManagerTests.cs
**Main test file** covering all core SQLManager functionality:

#### Connection Management
- ConnectAsync / DisconnectAsync
- Connection validation
- State management
- Connection events

#### Health Checks
- Full health checks with database stats
- Quick health checks
- Database statistics retrieval

#### CRUD Operations
- Add / AddRange
- Update / UpdateRange  
- Delete / DeleteRange / DeleteAll
- AddOrUpdate (upsert)
- GetById / GetAll

#### Read Operations
- WhereAsync with predicates
- AnyAsync / AnyExistAsync
- CountAsync with optional filters
- FirstOrDefaultAsync
- GetPagedAsync with ordering
- ExecuteQueryAsync (custom queries)
- ExecuteQueryNoTrackingAsync
- ExecuteSingleQueryAsync
- ExecuteAggregateAsync

#### Transactions
- ExecuteInTransactionAsync (void and with result)
- ExecuteBatchInTransactionAsync
- ExecuteReadTransactionAsync
- ExecuteComplexTransactionAsync

#### Concurrency Tests
- **Concurrent reads** (10 simultaneous read operations)
- **Concurrent writes** (10 simultaneous write operations)
- **Mixed reads and writes** (5 read + 3 write tasks)
- **Concurrent pagination** (10 pages read in parallel)
- **Concurrent transactions** (5 isolated transactions)
- **High volume operations** (50+ concurrent mixed operations)
- **Health checks during load** (10 health checks while performing 50 operations)
- **Stress test** (100+ concurrent operations with 95% success rate requirement)

#### Authorization
- IsSavable validation for new/existing entities

**Total: ~80 tests**

---

### 2. SQLManagerAdvancedTests.cs
**Advanced scenarios, edge cases, and performance tests:**

#### Performance Tests
- Bulk insert (10,000 entities in <5s)
- Bulk update (5,000 entities in <5s)
- Complex queries with multiple filters
- Pagination through large datasets (5,000 entities)

#### Edge Cases
- Duplicate data handling
- Empty result sets
- Non-existent entity updates
- Transaction rollbacks
- Invalid page numbers
- Already deleted entities

#### Complex Concurrency Scenarios
- Concurrent updates to same entity (conflict handling)
- Dependent transactions
- Long-running transactions with concurrent reads
- Pagination during active writes

#### Database File Operations
- WAL file verification after writes
- Flush effects on WAL size
- Disconnect/reconnect data persistence

#### Complex Queries
- Complex joins and filters
- Multiple aggregations (SUM, AVG, etc.)
- NoTracking behavior verification

#### Error Recovery
- Multiple failed transactions
- Connection stability after errors

#### Boundary Tests
- Empty strings
- Very long strings (10,000 chars)
- Negative values
- Max integer values

**Total: ~40 tests**

---

### 3. SQLManagerWALTests.cs
**WAL mode specific and concurrency configuration tests:**

#### WAL Configuration
- Journal mode verification (must be WAL)
- Busy timeout (5000ms)
- Cache size configuration
- Synchronous mode
- Locking mode
- Foreign keys
- Reconfiguration after disconnect

#### WAL File Operations
- WAL file existence after writes
- Checkpoint operations
- Flush behavior

#### Concurrent Read Tests
- 20 simultaneous reads
- Continuous reads during writes
- High volume reads (50 reads + 5 writes)

#### Write Serialization
- Sequential writes
- Concurrent write serialization
- Mixed read/write load (30 reads + 10 writes + 10 queries)

#### Transaction Concurrency
- Read transactions during slow writes
- Multiple write transactions serialization

#### Connection Stability
- Connection validation during heavy load
- Health checks during 50 concurrent operations

#### Performance with WAL
- Batch inserts (5,000 entities in <3s)
- Concurrent reads of large dataset (10,000 entities, 20 readers in <5s)

#### Database Statistics
- Statistics retrieval with WAL info

#### Cleanup and Maintenance
- Proper cleanup after heavy use
- Raw SQL checkpoint commands

**Total: ~30 tests**

---

## Test Infrastructure

### TestEntity
Simple entity for testing:
```csharp
public class TestEntity : IBaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Value { get; set; }
}
```

### TestDbContext
In-memory SQLite context for testing with proper configuration.

### TestDbContextFactory
Factory implementing `IDbContextFactory<RootDbContext>` for creating test contexts.

### AdminAuthorization
Authorization manager that allows all operations (Create, Read, Update, Delete).

---

## Running the Tests

### All Tests
```bash
dotnet test
```

### Specific Collection
```bash
dotnet test --filter "FullyQualifiedName~SQLManagerTests"
dotnet test --filter "FullyQualifiedName~SQLManagerAdvancedTests"
dotnet test --filter "FullyQualifiedName~SQLManagerWALTests"
```

### Specific Test
```bash
dotnet test --filter "FullyQualifiedName~ConcurrentReads_ShouldAllSucceed"
```

---

## Key Testing Principles

### 1. **No Internal Testing**
Tests only use the public `SQLManager` API, not internal components like `SQLConnectionManager` directly.

### 2. **Real Concurrency**
All concurrency tests use actual `Task.Run` and `Task.WhenAll` to test real concurrent scenarios.

### 3. **Performance Benchmarks**
Performance tests include actual time constraints to catch regressions.

### 4. **Cleanup**
Each test class uses `IAsyncLifetime` for proper setup/teardown:
- Creates unique temp database
- Cleans up after each test
- Handles disposal gracefully

### 5. **Comprehensive Coverage**
- ✅ All CRUD operations
- ✅ All read operations and queries
- ✅ All transaction types
- ✅ Connection lifecycle
- ✅ Health monitoring
- ✅ WAL configuration
- ✅ Concurrency at scale
- ✅ Edge cases and boundaries
- ✅ Error recovery

---

## Expected Behavior

### WAL Mode
When connected, SQLManager automatically configures:
- `journal_mode=WAL` (Write-Ahead Logging)
- `busy_timeout=5000` (5 second timeout)
- `cache_size=-20000` (20MB cache)
- `synchronous=NORMAL` (balanced safety/performance)
- `locking_mode=NORMAL` (allows concurrency)
- `foreign_keys=ON`

### Concurrency Model
- **Multiple Readers**: Up to 5 concurrent reads (semaphore limit)
- **Single Writer**: Write operations are serialized (1 writer at a time)
- **Read During Write**: WAL mode allows reads while writing
- **Transaction Isolation**: Each transaction is isolated

### Performance Targets
- Bulk insert 10,000 entities: < 5 seconds
- Bulk update 5,000 entities: < 5 seconds
- Paginate through 5,000 entities: < 2 seconds
- 100+ concurrent operations: > 95% success rate

---

## Maintenance

### Adding New Tests
1. Choose the appropriate file based on test type
2. Follow existing patterns for setup/teardown
3. Use descriptive test names following pattern: `Method_Scenario_ExpectedBehavior`
4. Add concurrency tests for operations that may be used concurrently

### Debugging Failed Tests
1. Check test output for specific error messages
2. Review connection state and database files in temp directory
3. Use `dotnet test --logger "console;verbosity=detailed"` for more info
4. Verify WAL configuration if concurrency tests fail

---

## Statistics

- **Total Tests**: ~150
- **Concurrency Tests**: ~30
- **Performance Tests**: ~10
- **Edge Case Tests**: ~15
- **WAL-Specific Tests**: ~15
- **Test Collections**: 3
- **Code Coverage Target**: >90%

---

## Notes

### Temporary Files
Tests create temporary databases in:
- Windows: `%TEMP%\SQLiteTests\*`
- Linux/Mac: `/tmp/SQLiteTests/*`

These are automatically cleaned up after tests complete.

### Test Isolation
Each test class runs in its own collection to avoid database conflicts. Tests within a class share a database instance but are designed to not interfere with each other.

### Async Patterns
All tests are async and use proper `await` patterns. No `.Result` or `.Wait()` calls that could cause deadlocks.
