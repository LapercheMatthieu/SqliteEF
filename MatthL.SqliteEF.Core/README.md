# MatthL.SqliteEF

[![NuGet](https://img.shields.io/nuget/v/MatthL.SqliteEF.svg)](https://www.nuget.org/packages/MatthL.SqliteEF/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**Zero boilerplate SQLite + Entity Framework for desktop applications**

Stop writing the same repository pattern code over and over. SqliteEF handles all the repetitive CRUD operations, connection management, and error handling so you can focus on your actual business logic.

## 🎯 The Problem

Every MVVM desktop app with SQLite needs the same boring code:
- Repository classes for each entity
- Connection management
- Transaction handling  
- CRUD operations
- Error handling
- Health checks

## ✨ The Solution

```csharp
// That's it. Seriously.
var manager = new SQLManager(() => new YourDbContext(), "data", "myapp");
await manager.Create();

// All CRUD operations ready to go
var people = await manager.GetAllAsync<Person>();
await manager.AddAsync(newPerson);
await manager.UpdateAsync(existingPerson);
await manager.DeleteAsync(oldPerson);
```

## 📦 What's Included

### Core Features
- 🚀 **Instant CRUD** - Generic operations for all your entities
- 📦 **Configured entities** - Configure your entities directly in the right place (themselves !!)
- 🔌 **Smart Connection Management** - Automatic connection state tracking
- 💾 **Transaction Support** - Built-in transaction wrapper
- 🛡️ **Authorization System** - Pluggable authorization via `IAuthorizationManager`
- ❤️ **Health Checks** - Monitor database health and performance
- 📊 **Result Pattern** - Clean error handling with `Result<T>` from MatthL.ResultLogger
- 🧠 **In-Memory Support** - Perfect for testing

### Architecture
```
SQLManager (Your single entry point)
├── SQLConnectionManager (Handles connections & transactions)
├── SQLDatabaseManager (Database creation/deletion)
├── SQLCrudOperations (All CRUD operations)
└── SQLHealthChecker (Database health monitoring)
```

## 🚀 Quick Start

### 1. Install
Working with the Result logger package allowing a quick overview of operations with the simple LogViewer
```bash
dotnet add package MatthL.SqliteEF
dotnet add package MatthL.ResultLogger
```

### 2. Create Your DbContext
Just your dbsets nothing more. 
```csharp
public class AppDbContext : RootDbContext
{
    public DbSet<Person> People { get; set; }
    public DbSet<Product> Products { get; set; }
}
```

### 3. Define Your Entities
Configure it directly below your properties what could be better ?
```csharp
public class Person : IBaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }

    public void ConfigureEntity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>()
            .HasKey(p => p.Id);
    }
}
```

### 4. Start Using
```csharp
// Initialize
var manager = new SQLManager(
    () => new AppDbContext(), 
    "C:/AppData/MyApp", 
    "database"
);

// Create database or open the existing one or migrate (same action)
await manager.Create();

// Use it!
var result = await manager.AddAsync(new Person { Name = "John", Age = 30 });
if (result.IsSuccess)
{
    var allPeople = await manager.GetAllAsync<Person>();
}
```

## 🔧 Advanced Features

### Custom Authorization
awailable : Admin, ReadOnly, WriteOnly and your own interface implementation
```csharp
public class MyAuthManager : IAuthorizationManager
{
    public bool CanCreate(string tableName) => User.HasPermission(tableName, "create");
    public bool CanRead(string tableName) => true;
    public bool CanUpdate(string tableName) => User.IsAdmin;
    public bool CanDelete(string tableName) => User.IsAdmin;
}

var manager = new SQLManager(
    () => new AppDbContext(),
    "data",
    "myapp",
    new MyAuthManager()
);
```

### Transactions
If you want efficiencies and safety
```csharp
var result = await manager.ExecuteInTransactionAsync(async () =>
{
    await manager.AddAsync(person1);
    await manager.AddAsync(person2);
    await manager.UpdateAsync(person3);
});
```

### Health Monitoring
```csharp
var health = await manager.CheckHealthAsync();
Console.WriteLine($"Status: {health.Status}");
Console.WriteLine($"Ping: {health.Details["pingMs"]}ms");
Console.WriteLine($"DB Size: {health.Details["dbSizeMB"]}MB");
```

### Connection State Events
```csharp
manager.ConnectionStateChanged += (sender, state) =>
{
    Console.WriteLine($"Connection state: {state}");
};
```

### Batch Operations
```csharp
var people = new List<Person> { /* ... */ };
await manager.AddRangeAsync(people);
await manager.UpdateRangeAsync(people);
await manager.DeleteRangeAsync(people);
```

### In-Memory Database (Testing)
```csharp
// Perfect for unit tests
var manager = new SQLManager(() => new TestDbContext());
await manager.Create();
// No files created, everything in memory
```

## 📊 Error Handling

All operations return `Result<T>` from MatthL.ResultLogger:

```csharp
var result = await manager.GetByIdAsync<Person>(1);

if (result.IsSuccess)
{
    var person = result.Value;
    // Use person
}
else
{
    Console.WriteLine($"Error: {result.Message}");
}
```

## 🎮 Complete Example

```csharp
public class PersonViewModel : ObservableObject
{
    private readonly SQLManager _db;
    
    public PersonViewModel()
    {
        _db = new SQLManager(() => new AppDbContext(), "data", "people");
        InitializeAsync();
    }
    
    private async void InitializeAsync()
    {
        await _db.Create();
        await LoadPeople();
    }
    
    public async Task LoadPeople()
    {
        var result = await _db.GetAllAsync<Person>();
        if (result.IsSuccess)
        {
            People = new ObservableCollection<Person>(result.Value);
        }
    }
    
    public async Task AddPerson(string name, int age)
    {
        var person = new Person { Name = name, Age = age };
        var result = await _db.AddAsync(person);
        
        if (result.IsSuccess)
        {
            await LoadPeople();
        }
    }
}
```

## 🔍 Why SqliteEF?

### Without SqliteEF (The Old Way)
```csharp
public class PersonRepository
{
    private readonly AppDbContext _context;
    
    public PersonRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<Person>> GetAllAsync()
    {
        try
        {
            return await _context.People.ToListAsync();
        }
        catch (Exception ex)
        {
            // Handle error
            return new List<Person>();
        }
    }
    
    public async Task AddAsync(Person person)
    {
        try
        {
            _context.People.Add(person);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Handle error
        }
    }
    
    // ... repeat for Update, Delete, etc.
}

// Repeat this entire class for EVERY entity 😭
```

### With SqliteEF (The Smart Way)
```csharp
var manager = new SQLManager(() => new AppDbContext(), "data", "myapp");
await manager.Create();

// ALL entities get CRUD operations automatically! 🎉
await manager.GetAllAsync<Person>();
await manager.GetAllAsync<Product>();
await manager.GetAllAsync<Order>();
// No more repository classes!
```

## 📋 Requirements

- .NET 6.0+ or .NET Framework 4.7.2+
- Entity Framework Core 6.0+
- Microsoft.Data.Sqlite

## 🚦 Roadmap

- ✅ v0.9 - Core CRUD operations, connection management, health checks
- 🔜 v1.0 - **Automatic repository synchronization** - Say goodbye to manual sync code!
- 🔜 v1.1 - Bulk operations optimization
- 🔜 v1.2 - Advanced query builder

## 🤝 Contributing

Found a bug? Want a feature? Open an issue or submit a PR!

## 📄 License

MIT License - see LICENSE file for details.

## 💡 Tips & Tricks

### Performance Optimization
```csharp
// Use transactions for bulk operations
await manager.ExecuteInTransactionAsync(async () =>
{
    foreach (var item in largeCollection)
    {
        await manager.AddAsync(item);
    }
});
```

### Testing Pattern
```csharp
[TestClass]
public class PersonServiceTests
{
    private SQLManager _manager;
    
    [TestInitialize]
    public async Task Setup()
    {
        _manager = new SQLManager(() => new TestDbContext()); // In-memory
        await _manager.Create();
    }
    
    [TestMethod]
    public async Task CanAddPerson()
    {
        var person = new Person { Name = "Test", Age = 25 };
        var result = await _manager.AddAsync(person);
        
        Assert.IsTrue(result.IsSuccess);
    }
}
```

### Connection Pooling
```csharp
// SQLManager handles connection pooling automatically
// Just use it - no need to worry about connection management
```

<div align="center">

---

**Stop writing boilerplate. Start building features.**

Made with ❤️ by [Matthieu L](https://github.com/LapercheMatthieu)

</div>
