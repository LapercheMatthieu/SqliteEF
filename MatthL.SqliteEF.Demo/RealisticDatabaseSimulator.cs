using MatthL.ResultLogger.Core.Models;
using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Demo.DBContexts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Demo
{
    /// <summary>
    /// Wrapper autour du vrai SQLManager qui simule de l'activité réaliste
    /// </summary>
    public class RealisticDatabaseSimulator : IDisposable
    {
        private readonly SQLManager _realManager;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Random _random = new Random();
        private Task _simulationTask;
        private bool _isSimulating;

        public SQLManager Manager => _realManager;

        private RealisticDatabaseSimulator(SQLManager manager)
        {
            _realManager = manager;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Crée une base de données réelle et lance la simulation d'activité
        /// </summary>
        public static async Task<RealisticDatabaseSimulator> CreateAsync(string fileName, string folderPath, string extension = ".db")
        {
            // Créer le dossier s'il n'existe pas
            Directory.CreateDirectory(folderPath);

            // Créer le vrai SQLManager
            var manager = new SQLManager(
                contextFactory: (path) => new PersonDBContext(path),
                folderPath: folderPath,
                fileName: fileName,
                extension: extension
            );

            // Créer la base de données
            var createResult = await manager.Create();
            if (createResult.IsFailure)
            {
                throw new Exception($"Échec de la création de la DB: {createResult.Error}");
            }

            // Connecter
            var connectResult = await manager.ConnectAsync();
            if (connectResult.IsFailure)
            {
                throw new Exception($"Échec de la connexion: {connectResult.Error}");
            }

            // Peupler avec des données initiales
            await PopulateInitialDataAsync(manager);

            var simulator = new RealisticDatabaseSimulator(manager);
            simulator.StartSimulation();

            return simulator;
        }

        /// <summary>
        /// Crée une base de données déconnectée (sans simulation)
        /// </summary>
        public static async Task<RealisticDatabaseSimulator> CreateDisconnectedAsync(string fileName, string folderPath, string extension = ".db")
        {
            Directory.CreateDirectory(folderPath);

            var manager = new SQLManager(
                contextFactory: (path) => new PersonDBContext(path),
                folderPath: folderPath,
                fileName: fileName,
                extension: extension
            );

            // Créer la base mais ne pas se connecter
            await manager.Create();
            await manager.DisconnectAsync();

            return new RealisticDatabaseSimulator(manager);
        }

        /// <summary>
        /// Peuple la base avec des données initiales
        /// </summary>
        private static async Task PopulateInitialDataAsync(SQLManager manager)
        {
            var names = new[]
            {
                "Alice Johnson", "Bob Smith", "Charlie Brown", "Diana Prince",
                "Edward Norton", "Fiona Apple", "George Martin", "Hannah Montana",
                "Ian McKellen", "Julia Roberts", "Kevin Spacey", "Laura Palmer",
                "Michael Jordan", "Nancy Drew", "Oliver Twist", "Patricia Arquette",
                "Quentin Tarantino", "Rachel Green", "Steven Spielberg", "Tina Turner"
            };

            var persons = names.Select(name => new Person { Name = name }).ToList();

            await manager.AddRangeAsync(persons);
        }

        /// <summary>
        /// Lance la simulation d'activité en arrière-plan
        /// </summary>
        private void StartSimulation()
        {
            _isSimulating = true;
            _simulationTask = Task.Run(async () => await SimulateActivityAsync(_cancellationTokenSource.Token));
        }

        /// <summary>
        /// Simule de l'activité réaliste sur la base de données
        /// </summary>
        private async Task SimulateActivityAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _realManager.IsConnected)
            {
                try
                {
                    // Attendre un délai aléatoire entre les opérations (2-8 secondes)
                    await Task.Delay(_random.Next(2000, 8000), cancellationToken);

                    // Choisir une opération aléatoire
                    var operation = _random.Next(0, 100);

                    if (operation < 40) // 40% - Lecture simple
                    {
                        await PerformReadOperationAsync();
                    }
                    else if (operation < 70) // 30% - Lecture avec filtre
                    {
                        await PerformFilteredReadAsync();
                    }
                    else if (operation < 85) // 15% - Ajout
                    {
                        await PerformAddOperationAsync();
                    }
                    else if (operation < 95) // 10% - Mise à jour
                    {
                        await PerformUpdateOperationAsync();
                    }
                    else // 5% - Suppression
                    {
                        await PerformDeleteOperationAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Log l'erreur mais continue la simulation
                    Console.WriteLine($"Erreur simulation: {ex.Message}");
                }
            }
        }

        #region Opérations réalistes

        private async Task PerformReadOperationAsync()
        {
            // Lecture de toutes les personnes
            var result = await _realManager.GetAllAsync<Person>();
            Console.WriteLine($"📖 Lecture: {result.Value?.Count ?? 0} personnes trouvées");
        }

        private async Task PerformFilteredReadAsync()
        {
            // Lecture avec filtre (noms commençant par une lettre aléatoire)
            var letter = (char)('A' + _random.Next(0, 26));
            var result = await _realManager.WhereAsync<Person>(p => p.Name.StartsWith(letter.ToString()));
            Console.WriteLine($"🔍 Recherche (lettre {letter}): {result.Value?.Count ?? 0} résultats");
        }

        private async Task PerformAddOperationAsync()
        {
            // Ajout d'une nouvelle personne
            var firstNames = new[] { "John", "Jane", "Mike", "Emma", "Chris", "Sarah", "David", "Lisa" };
            var lastNames = new[] { "Doe", "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller" };

            var newPerson = new Person
            {
                Name = $"{firstNames[_random.Next(firstNames.Length)]} {lastNames[_random.Next(lastNames.Length)]}"
            };

            var result = await _realManager.AddAsync(newPerson);
            if (result.IsSuccess)
            {
                Console.WriteLine($"➕ Ajout: {newPerson.Name}");
            }
        }

        private async Task PerformUpdateOperationAsync()
        {
            // Mise à jour d'une personne aléatoire
            var allResult = await _realManager.GetAllAsync<Person>();
            if (allResult.IsSuccess && allResult.Value.Any())
            {
                var randomPerson = allResult.Value[_random.Next(allResult.Value.Count)];
                var oldName = randomPerson.Name;
                randomPerson.Name = $"{randomPerson.Name} (modifié {DateTime.Now:HH:mm:ss})";

                var updateResult = await _realManager.UpdateAsync(randomPerson);
                if (updateResult.IsSuccess)
                {
                    Console.WriteLine($"✏️ Mise à jour: {oldName} → {randomPerson.Name}");
                }
            }
        }

        private async Task PerformDeleteOperationAsync()
        {
            // Suppression d'une personne aléatoire (mais garder au moins 5 personnes)
            var countResult = await _realManager.CountAsync<Person>();
            if (countResult.IsSuccess && countResult.Value > 5)
            {
                var allResult = await _realManager.GetAllAsync<Person>();
                if (allResult.IsSuccess && allResult.Value.Any())
                {
                    var randomPerson = allResult.Value[_random.Next(allResult.Value.Count)];
                    var deleteResult = await _realManager.DeleteAsync(randomPerson);
                    if (deleteResult.IsSuccess)
                    {
                        Console.WriteLine($"🗑️ Suppression: {randomPerson.Name}");
                    }
                }
            }
        }

        #endregion

        #region Opérations manuelles pour la démo

        /// <summary>
        /// Ajoute plusieurs personnes d'un coup
        /// </summary>
        public async Task<Result> AddMultiplePeopleAsync(int count)
        {
            var names = new[] { "Alex", "Blake", "Casey", "Drew", "Emerson", "Finley", "Gray", "Harper" };
            var people = Enumerable.Range(0, count)
                .Select(i => new Person { Name = $"{names[_random.Next(names.Length)]} Person{i}" })
                .ToList();

            return await _realManager.AddRangeAsync(people);
        }

        /// <summary>
        /// Effectue une transaction complexe
        /// </summary>
        public async Task<Result> PerformComplexTransactionAsync()
        {
            return await _realManager.ExecuteInTransactionAsync(async context =>
            {
                // Ajouter 3 personnes
                var newPeople = new[]
                {
                    new Person { Name = "Transaction User 1" },
                    new Person { Name = "Transaction User 2" },
                    new Person { Name = "Transaction User 3" }
                };

                context.Set<Person>().AddRange(newPeople);
                await context.SaveChangesAsync();

                // Simuler un délai
                await Task.Delay(500);

                Console.WriteLine("✅ Transaction complexe réussie: 3 personnes ajoutées");
            });
        }

        /// <summary>
        /// Nettoie les personnes "modifiées"
        /// </summary>
        public async Task<Result<int>> CleanupModifiedPeopleAsync()
        {
            var result = await _realManager.WhereAsync<Person>(p => p.Name.Contains("(modifié"));
            if (result.IsSuccess)
            {
                var count = result.Value.Count;
                await _realManager.DeleteRangeAsync(result.Value);
                Console.WriteLine($"🧹 Nettoyage: {count} personnes modifiées supprimées");
                return Result<int>.Success(count);
            }
            return Result<int>.Failure("Échec du nettoyage");
        }

        /// <summary>
        /// Obtient des statistiques sur les données
        /// </summary>
        public async Task<Result<string>> GetDataStatisticsAsync()
        {
            var countResult = await _realManager.CountAsync<Person>();
            var allResult = await _realManager.GetAllAsync<Person>();

            if (countResult.IsSuccess && allResult.IsSuccess)
            {
                var stats = $"Total: {countResult.Value} personnes\n" +
                           $"Modifiées: {allResult.Value.Count(p => p.Name.Contains("(modifié"))}\n" +
                           $"Noms les plus longs: {allResult.Value.OrderByDescending(p => p.Name.Length).Take(3).Select(p => p.Name).Aggregate((a, b) => $"{a}, {b}")}";

                return Result<string>.Success(stats);
            }

            return Result<string>.Failure("Impossible d'obtenir les statistiques");
        }

        #endregion

        public void Dispose()
        {
            _isSimulating = false;
            _cancellationTokenSource?.Cancel();
            _simulationTask?.Wait(TimeSpan.FromSeconds(2));
            _cancellationTokenSource?.Dispose();

            // Ne pas disposer le manager ici, il sera géré par l'appelant
        }
    }
}
