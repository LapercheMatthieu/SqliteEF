using Microsoft.EntityFrameworkCore;
using SQLiteManager.Authorizations;
using SQLiteManager.Managers;
using SQLiteManager.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteManager.Demo
{
    // Entités de test
    public class TestPerson : IBaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestPerson>()
                .ToTable("People")
                .HasKey(e => e.Id);

            modelBuilder.Entity<TestPerson>()
                .Property(e => e.Name)
                .IsRequired();
        }
    }

    public class TestOrder : IBaseEntity
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public decimal Total { get; set; }
        public int PersonId { get; set; }
        public TestPerson Person { get; set; }

        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestOrder>()
                .ToTable("Orders")
                .HasKey(e => e.Id);

            modelBuilder.Entity<TestOrder>()
                .Property(e => e.OrderNumber)
                .IsRequired();

            modelBuilder.Entity<TestOrder>()
                .HasOne(o => o.Person)
                .WithMany()
                .HasForeignKey(o => o.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    // Context de test
    public class TestDbContext : RootDbContext
    {
        public DbSet<TestPerson> People { get; set; }
        public DbSet<TestOrder> Orders { get; set; }
    }

    // Programme de test
    public class SQLiteManagerTest
    {
        private readonly string _testFolder;
        private readonly string _testDbName;
        private SQLManager _sqlManager;
        private TestDbContext _dbContext;

        public SQLiteManagerTest()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "SQLiteManagerTest_" + Guid.NewGuid().ToString());
            _testDbName = "test_database.db";
            Directory.CreateDirectory(_testFolder);

            Debug.WriteLine($"Test folder: {_testFolder}");
            Debug.WriteLine($"Test database: {_testDbName}");
        }

        private async Task Initialize()
        {
            _dbContext = new TestDbContext();
            //_sqlManager = new SQLManager(_dbContext, _testFolder, _testDbName, new AdminAuthorization());
            _sqlManager = new SQLManager(_dbContext);
            //await _sqlManager.Create();
            await _dbContext.Database.EnsureCreatedAsync();

        }

        private async Task Cleanup()
        {
            await _sqlManager.DisposeAsync();

            try
            {
                string dbPath = Path.Combine(_testFolder, _testDbName);
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }

                string walPath = dbPath + "-wal";
                if (File.Exists(walPath))
                {
                    File.Delete(walPath);
                }

                string shmPath = dbPath + "-shm";
                if (File.Exists(shmPath))
                {
                    File.Delete(shmPath);
                }

                Directory.Delete(_testFolder, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        public async Task RunAllTests()
        {
            try
            {
                Debug.WriteLine("Starting SQLiteManager tests...\n");

                await Initialize();

                await TestCreateDatabase();
                await TestAddEntity();
                await TestAddMultipleEntities();
                await TestCreateDatabase2();
                await _sqlManager.CloseConnection();
                await TestGetAllEntities();
                await TestGetEntityById();
                await TestUpdateEntity();
                await TestAddOrUpdateEntity();
                await TestRelationships();
                await TestDeleteEntity();
                await TestDeleteAllEntities();

                Debug.WriteLine("\nAll tests completed successfully!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"\nTest failed with error: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                await Cleanup();
            }
        }

        private async Task TestCreateDatabase()
        {
            Debug.WriteLine("Testing database creation...");

            string dbPath = Path.Combine(_testFolder, _testDbName);
            bool dbExists = File.Exists(dbPath);

            AssertTrue(dbExists, "Database file should exist after creation");
            Debug.WriteLine("✓ Database creation test passed");
        }
        private async Task Initialize2()
        {
            // Assurez-vous que toutes les connexions sont fermées
            await _sqlManager.CloseConnection();

            // Disposer complètement de l'ancien gestionnaire et de son contexte
            await _sqlManager.DisposeAsync();

            // Forcer le nettoyage de la mémoire
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Créer un nouveau DbContext
            _dbContext = new TestDbContext();

            // Créer un nouveau gestionnaire avec le nouveau nom de fichier
            _sqlManager = new SQLManager(_dbContext, _testFolder, "Test2.db", new AdminAuthorization());

            // Créer la base de données
            var success = await _sqlManager.Create();

            if (!success)
            {
                Debug.WriteLine("AVERTISSEMENT: success est false pour la création de la DB2");

                // Vérifier si le fichier a bien été créé malgré tout
                string dbPath = Path.Combine(_testFolder, "Test2.db");
                if (File.Exists(dbPath))
                {
                    Debug.WriteLine("Le fichier de base de données existe même si success est false");
                }
                else
                {
                    Debug.WriteLine("Le fichier de base de données n'existe pas");
                }
            }

        }
        private async Task TestCreateDatabase2()
        {
            await Initialize2();
            Debug.WriteLine("Testing database creation2...");

            string dbPath = Path.Combine(_testFolder, _testDbName + "2");
            bool dbExists = File.Exists(dbPath);

            AssertTrue(dbExists, "Database file should exist after creation");
            Debug.WriteLine("✓ Database creation test passed");
        }

        private async Task TestAddEntity()
        {
            Debug.WriteLine("Testing adding a single entity...");

            var person = new TestPerson { Name = "John Doe", Age = 30 };
            bool result = await _sqlManager.AddAsync(person);

            AssertTrue(result, "Add operation should return true");
            AssertTrue(person.Id > 0, "Entity should have an ID after being added");

            Debug.WriteLine("✓ Add entity test passed");
        }

        private async Task TestAddMultipleEntities()
        {
            Debug.WriteLine("Testing adding multiple entities...");

            var people = new List<TestPerson>
            {
                new TestPerson { Name = "Jane Smith", Age = 25 },
                new TestPerson { Name = "Bob Johnson", Age = 40 }
            };

            bool result = await _sqlManager.AddRangeAsync(people);

            AssertTrue(result, "AddRange operation should return true");
            AssertTrue(people.All(p => p.Id > 0), "All entities should have IDs after being added");

            Debug.WriteLine("✓ Add multiple entities test passed");
        }

        private async Task TestGetAllEntities()
        {
            Debug.WriteLine("Testing retrieving all entities...");

            var people = await _sqlManager.GetAllAsync<TestPerson>();

            AssertTrue(people.Count() >= 3, "Should retrieve at least 3 people");

            Debug.WriteLine("✓ Get all entities test passed");
        }

        private async Task TestGetEntityById()
        {
            Debug.WriteLine("Testing retrieving entity by ID...");

            var people = await _sqlManager.GetAllAsync<TestPerson>();
            int firstPersonId = people.First().Id;

            var person = await _sqlManager.GetByIdAsync<TestPerson>(firstPersonId);

            AssertNotNull(person, "Should retrieve the person by ID");
            AssertEquals(firstPersonId, person.Id, "Retrieved person should have the requested ID");

            Debug.WriteLine("✓ Get entity by ID test passed");
        }

        private async Task TestUpdateEntity()
        {
            Debug.WriteLine("Testing updating an entity...");

            var people = await _sqlManager.GetAllAsync<TestPerson>();
            var personToUpdate = people.First();
            string originalName = personToUpdate.Name;
            string newName = originalName + " Updated";

            personToUpdate.Name = newName;
            bool result = await _sqlManager.UpdateAsync(personToUpdate);

            AssertTrue(result, "Update operation should return true");

            var updatedPerson = await _sqlManager.GetByIdAsync<TestPerson>(personToUpdate.Id);
            AssertEquals(newName, updatedPerson.Name, "Name should be updated");

            Debug.WriteLine("✓ Update entity test passed");
        }

        private async Task TestAddOrUpdateEntity()
        {
            Debug.WriteLine("Testing add or update entity...");

            // Test update case
            var people = await _sqlManager.GetAllAsync<TestPerson>();
            var existingPerson = people.First();
            existingPerson.Age += 1;

            bool updateResult = await _sqlManager.AddOrUpdateAsync(existingPerson);
            AssertTrue(updateResult, "AddOrUpdate (update case) should return true");

            var updatedPerson = await _sqlManager.GetByIdAsync<TestPerson>(existingPerson.Id);
            AssertEquals(existingPerson.Age, updatedPerson.Age, "Age should be updated");

            // Test add case
            var newPerson = new TestPerson { Name = "AddOrUpdate Test", Age = 50 };
            bool addResult = await _sqlManager.AddOrUpdateAsync(newPerson);

            AssertTrue(addResult, "AddOrUpdate (add case) should return true");
            AssertTrue(newPerson.Id > 0, "New entity should have an ID after being added");

            Debug.WriteLine("✓ Add or update entity test passed");
        }

        private async Task TestRelationships()
        {
            Debug.WriteLine("Testing entity relationships...");

            var people = await _sqlManager.GetAllAsync<TestPerson>();
            var person = people.First();

            var order = new TestOrder
            {
                OrderNumber = "ORD-001",
                Total = 99.99m,
                PersonId = person.Id
            };

            bool result = await _sqlManager.AddAsync(order);
            AssertTrue(result, "Adding order should return true");

            var orders = await _sqlManager.GetAllAsync<TestOrder>();
            AssertTrue(orders.Count() > 0, "Should have at least one order");

            var retrievedOrder = orders.First();
            AssertEquals(person.Id, retrievedOrder.PersonId, "Order should reference the correct person");

            Debug.WriteLine("✓ Entity relationships test passed");
        }

        private async Task TestDeleteEntity()
        {
            Debug.WriteLine("Testing deleting an entity...");

            var people = await _sqlManager.GetAllAsync<TestPerson>();
            int initialCount = people.Count();
            var personToDelete = people.Last();

            bool result = await _sqlManager.DeleteAsync(personToDelete);
            AssertTrue(result, "Delete operation should return true");

            var peopleAfterDelete = await _sqlManager.GetAllAsync<TestPerson>();
            AssertEquals(initialCount - 1, peopleAfterDelete.Count(), "Should have one less person after deletion");

            var deletedPerson = await _sqlManager.GetByIdAsync<TestPerson>(personToDelete.Id);
            AssertNull(deletedPerson, "Deleted person should not be retrievable");

            Debug.WriteLine("✓ Delete entity test passed");
        }

        private async Task TestDeleteAllEntities()
        {
            Debug.WriteLine("Testing deleting all entities of a type...");

            // First ensure we have orders to delete
            var orders = await _sqlManager.GetAllAsync<TestOrder>();
            if (orders.Count() == 0)
            {
                var people = await _sqlManager.GetAllAsync<TestPerson>();
                if (people.Count() > 0)
                {
                    var newOrder = new TestOrder
                    {
                        OrderNumber = "ORD-999",
                        Total = 199.99m,
                        PersonId = people.First().Id
                    };
                    await _sqlManager.AddAsync(newOrder);
                }
            }

            bool result = await _sqlManager.DeleteAllAsync<TestOrder>();
            AssertTrue(result, "DeleteAll operation should return true");

            var ordersAfterDelete = await _sqlManager.GetAllAsync<TestOrder>();
            AssertEquals(0, ordersAfterDelete.Count(), "Should have no orders after DeleteAll");

            Debug.WriteLine("✓ Delete all entities test passed");
        }

        // Assert helpers
        private void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception($"Assertion failed: {message}");
        }

        private void AssertFalse(bool condition, string message)
        {
            if (condition)
                throw new Exception($"Assertion failed: {message}");
        }

        private void AssertEquals<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new Exception($"Assertion failed: {message}. Expected: {expected}, Actual: {actual}");
        }

        private void AssertNotNull(object obj, string message)
        {
            if (obj == null)
                throw new Exception($"Assertion failed: {message}. Object is null.");
        }

        private void AssertNull(object obj, string message)
        {
            if (obj != null)
                throw new Exception($"Assertion failed: {message}. Object is not null.");
        }
    }

    // Point d'entrée pour exécuter les tests
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var test = new SQLiteManagerTest();
            await test.RunAllTests();

            Debug.WriteLine("\nPress any key to exit...");
        }
    }
}
