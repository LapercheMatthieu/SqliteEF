using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

public class ImprovedDatabaseSeeder
{
    private readonly DbContext _context;
    private readonly Dictionary<Type, List<object>> _generatedEntities = new();
    private readonly Dictionary<Type, Faker> _fakers = new();
    private readonly Random _random = new();
    private string ErrorMessage;

    public ImprovedDatabaseSeeder(DbContext context)
    {
        _context = context;
        ConfigureFakers();
    }

    private void ConfigureFakers()
    {
        var entityTypes = _context.Model.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            var faker = new Faker();
            _fakers[entityType.ClrType] = faker;
        }
    }

    public void SeedData(int count = 10)
    {
        ErrorMessage = "";
        var orderedTypes = GetOrderedEntityTypes();

        foreach (var entityType in orderedTypes)
        {
            var entities = GenerateEntities(entityType, count);
            _generatedEntities[entityType] = entities;
            _context.AddRange(entities);

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                if(ex.InnerException != null)
                {
                    BuildMessage($"Erreur lors de la génération des données pour {entityType.Name}: {ex.InnerException.Message}");
                }
                else
                {
                    BuildMessage($"Erreur lors de la génération des données pour {entityType.Name}: {ex.Message}");
                }
            }
        }
        MessageBox.Show(ErrorMessage);
    }

    private void BuildMessage(string Message)
    {
            ErrorMessage = ErrorMessage + Message + "\n";
    }

    private IEnumerable<Type> GetOrderedEntityTypes()
    {
        var entityTypes = _context.Model.GetEntityTypes().ToList();
        var dependencies = new Dictionary<Type, HashSet<Type>>();

        // Construire le graphe de dépendances
        foreach (var entityType in entityTypes)
        {
            var requiredDependencies = entityType.GetForeignKeys()
                .Where(fk => fk.IsRequired)
                .Select(fk => fk.PrincipalEntityType.ClrType)
                .ToHashSet();

            dependencies[entityType.ClrType] = requiredDependencies;
        }

        // Tri topologique
        var sorted = new List<Type>();
        var visited = new HashSet<Type>();
        var processing = new HashSet<Type>();

        foreach (var type in dependencies.Keys)
        {
            if (!visited.Contains(type))
            {
                SortTypes(type, dependencies, visited, processing, sorted);
            }
        }

        return sorted;
    }

    private void SortTypes(Type type, Dictionary<Type, HashSet<Type>> dependencies,
        HashSet<Type> visited, HashSet<Type> processing, List<Type> sorted)
    {
        processing.Add(type);

        foreach (var dependency in dependencies[type])
        {
            if (processing.Contains(dependency))
            {
                throw new Exception($"Dépendance circulaire détectée pour {type.Name}");
            }

            if (!visited.Contains(dependency))
            {
                SortTypes(dependency, dependencies, visited, processing, sorted);
            }
        }

        processing.Remove(type);
        visited.Add(type);
        sorted.Add(type);
    }

    private List<object> GenerateEntities(Type entityType, int count)
    {
        var entities = new List<object>();
        var efEntityType = _context.Model.FindEntityType(entityType);
        var faker = _fakers[entityType];

        for (int i = 0; i < count; i++)
        {
            var entity = GenerateEntity(entityType, efEntityType, faker);
            entities.Add(entity);
        }

        return entities;
    }

    private object GenerateEntity(Type entityType, IEntityType efEntityType, Faker faker)
    {
        var entity = Activator.CreateInstance(entityType);
        var foreignKeys = efEntityType.GetForeignKeys().ToList();

        foreach (var property in entityType.GetProperties())
        {
            if (!property.CanWrite) continue;

            var efProperty = efEntityType.FindProperty(property.Name);

            // Skip auto-generated properties
            if (efProperty?.ValueGenerated == ValueGenerated.OnAdd ||
                efProperty?.ValueGenerated == ValueGenerated.OnAddOrUpdate)
            {
                continue;
            }

            // Check if this property is a navigation property
            var navigation = efEntityType.FindNavigation(property.Name);
            if (navigation != null)
            {
                if (!navigation.IsCollection)
                {
                    SetForeignKeyValue(entity, property, navigation);
                }
                continue;
            }

            // Check if this property is a foreign key property
            var foreignKey = foreignKeys.FirstOrDefault(fk =>
                fk.Properties.Any(p => p.Name == property.Name));

            if (foreignKey != null)
            {
                // Get the related navigation property if it exists
                var relatedNavigation = foreignKey.DependentToPrincipal;
                if (relatedNavigation != null)
                {
                    // First set the related entity
                    var navigationProperty = entityType.GetProperty(relatedNavigation.Name);
                    SetForeignKeyValue(entity, navigationProperty, relatedNavigation);

                    // Then get the foreign key value from the related entity
                    var relatedEntity = navigationProperty.GetValue(entity);
                    if (relatedEntity != null)
                    {
                        var principalKey = foreignKey.PrincipalKey.Properties.First();
                        var keyValue = principalKey.PropertyInfo.GetValue(relatedEntity);
                        property.SetValue(entity, keyValue);
                    }
                    continue;
                }
                else
                {
                    // If there's no navigation property, generate a valid foreign key value
                    var principalType = foreignKey.PrincipalEntityType.ClrType;
                    if (_generatedEntities.ContainsKey(principalType) && _generatedEntities[principalType].Any())
                    {
                        var relatedEntity = _generatedEntities[principalType][_random.Next(_generatedEntities[principalType].Count)];
                        var principalKey = foreignKey.PrincipalKey.Properties.First();
                        var keyValue = principalKey.PropertyInfo.GetValue(relatedEntity);
                        property.SetValue(entity, keyValue);
                        continue;
                    }
                }
            }

            // If not a foreign key or navigation, generate a random value
            var value = GeneratePropertyValue(property.PropertyType, faker);
            property.SetValue(entity, value);
        }

        return entity;
    }

    private void SetForeignKeyValue(object entity, System.Reflection.PropertyInfo property, INavigation navigation)
    {
        var principalType = navigation.TargetEntityType.ClrType;

        if (_generatedEntities.ContainsKey(principalType) && _generatedEntities[principalType].Any())
        {
            var relatedEntities = _generatedEntities[principalType];
            var randomEntity = relatedEntities[_random.Next(relatedEntities.Count)];
            property.SetValue(entity, randomEntity);
        }
    }

    private object GeneratePropertyValue(Type propertyType, Faker faker)
    {
        if (propertyType == typeof(string))
            return faker.Lorem.Word();
        if (propertyType == typeof(int))
            return faker.Random.Int(1, 1000);
        if (propertyType == typeof(decimal))
            return faker.Random.Decimal(1, 1000);
        if (propertyType == typeof(DateTime))
            return faker.Date.Between(DateTime.Now.AddYears(-1), DateTime.Now);
        if (propertyType == typeof(bool))
            return faker.Random.Bool();
        if (propertyType == typeof(Guid))
            return Guid.NewGuid();
        if (propertyType.IsEnum)
        {
            var values = Enum.GetValues(propertyType);
            return values.GetValue(_random.Next(values.Length));
        }
        if (Nullable.GetUnderlyingType(propertyType) != null)
            return faker.Random.Bool() ? null : GeneratePropertyValue(Nullable.GetUnderlyingType(propertyType), faker);

        return null;
    }
}
