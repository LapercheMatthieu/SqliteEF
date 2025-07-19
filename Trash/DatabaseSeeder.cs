using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore.Metadata;

public class DatabaseSeeder
{
    private readonly Dictionary<Type, List<object>> _generatedEntities = new();
    private readonly DbContext _context;
    private readonly Random _random = new();
    private readonly int _defaultCount;

    public DatabaseSeeder(DbContext context, int defaultCount = 10)
    {
        _context = context;
        _defaultCount = defaultCount;
    }

    public void SeedDatabase()
    {
        var entityTypes = GetOrderedEntityTypes();

        foreach (var entityType in entityTypes)
        {
            GenerateEntitiesForType(entityType, _defaultCount);
            _context.SaveChanges(); // Sauvegarde après chaque type pour assurer les références
        }
    }

    private IEnumerable<Type> GetOrderedEntityTypes()
    {
        var entityTypes = _context.Model.GetEntityTypes().ToList();
        var sortedTypes = new List<IEntityType>();
        var visitedTypes = new HashSet<IEntityType>();
        var processingTypes = new HashSet<IEntityType>();

        foreach (var entityType in entityTypes)
        {
            if (!visitedTypes.Contains(entityType))
            {
                TopologicalSort(entityType, visitedTypes, processingTypes, sortedTypes);
            }
        }

        return sortedTypes.Select(e => e.ClrType);
    }

    private void TopologicalSort(IEntityType entityType, HashSet<IEntityType> visitedTypes,
        HashSet<IEntityType> processingTypes, List<IEntityType> sortedTypes)
    {
        processingTypes.Add(entityType);

        // Obtenir toutes les dépendances requises (clés étrangères obligatoires)
        var dependencies = entityType.GetForeignKeys()
            .Where(fk => fk.IsRequired)
            .Select(fk => fk.PrincipalEntityType);

        foreach (var dependency in dependencies)
        {
            if (processingTypes.Contains(dependency))
            {
                throw new Exception($"Détection d'une dépendance circulaire impliquant {entityType.Name}");
            }

            if (!visitedTypes.Contains(dependency))
            {
                TopologicalSort(dependency, visitedTypes, processingTypes, sortedTypes);
            }
        }

        processingTypes.Remove(entityType);
        visitedTypes.Add(entityType);
        sortedTypes.Add(entityType);
    }

    private void GenerateEntitiesForType(Type entityType, int count)
    {
        if (!_generatedEntities.ContainsKey(entityType))
        {
            _generatedEntities[entityType] = new List<object>();
        }

        var faker = new Faker();
        var entities = new List<object>();

        for (int i = 0; i < count; i++)
        {
            var entity = GenerateEntity(entityType, faker);
            entities.Add(entity);
        }

        _generatedEntities[entityType].AddRange(entities);
        _context.AddRange(entities);
    }

    private object GenerateEntity(Type entityType, Faker faker)
    {
        var entity = Activator.CreateInstance(entityType);
        var efEntityType = _context.Model.FindEntityType(entityType);

        foreach (var property in entityType.GetProperties())
        {
            // Ignorer les propriétés qui ne doivent pas être générées
            if (ShouldSkipProperty(property, efEntityType))
            {
                continue;
            }

            // Gérer les clés étrangères et les propriétés de navigation
            if (IsNavigation(property, efEntityType))
            {
                HandleNavigation(entity, property);
                continue;
            }

            if (!IsForeignKey(property, efEntityType))
            {
                var value = GenerateValueForProperty(property.PropertyType, faker);
                property.SetValue(entity, value);
            }
        }

        return entity;
    }

    private bool ShouldSkipProperty(System.Reflection.PropertyInfo property, IEntityType efEntityType)
    {
        // Ignorer les collections
        if (IsCollection(property.PropertyType))
            return true;

        // Ignorer les propriétés en lecture seule
        if (!property.CanWrite)
            return true;

        // Ignorer les clés primaires auto-générées
        var primaryKey = efEntityType.FindPrimaryKey();
        if (primaryKey != null && primaryKey.Properties.Any(p => p.Name == property.Name))
        {
            var valueGenerated = efEntityType.FindProperty(property.Name)?.ValueGenerated;
            return valueGenerated == ValueGenerated.OnAdd || valueGenerated == ValueGenerated.OnAddOrUpdate;
        }

        return false;
    }

    private bool IsCollection(Type type)
    {
        return type.IsGenericType && (
            type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
            type.GetGenericTypeDefinition() == typeof(List<>) ||
            type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ||
            type.GetGenericTypeDefinition() == typeof(ObservableCollection<>));
    }

    private bool IsNavigation(System.Reflection.PropertyInfo property, IEntityType entityType)
    {
        return entityType.FindNavigation(property.Name) != null;
    }

    private bool IsForeignKey(System.Reflection.PropertyInfo property, IEntityType entityType)
    {
        return entityType.GetForeignKeys().Any(fk =>
            fk.Properties.Any(p => p.Name == property.Name));
    }

    private void HandleNavigation(object entity, System.Reflection.PropertyInfo property)
    {
        var foreignKeyType = property.PropertyType;
        if (_generatedEntities.ContainsKey(foreignKeyType) && _generatedEntities[foreignKeyType].Any())
        {
            var foreignEntities = _generatedEntities[foreignKeyType];
            if (foreignEntities.Any())
            {
                property.SetValue(entity, foreignEntities[_random.Next(foreignEntities.Count)]);
            }
        }
    }

    private object GenerateValueForProperty(Type propertyType, Faker faker)
    {
        if (propertyType == typeof(string))
            return faker.Lorem.Word();

        if (propertyType == typeof(int))
            return faker.Random.Int(1, 1000);

        if (propertyType == typeof(decimal))
            return faker.Random.Decimal(1, 1000);

        if (propertyType == typeof(double))
            return faker.Random.Double(1, 1000);

        if (propertyType == typeof(DateTime))
            return faker.Date.Between(DateTime.Now.AddYears(-5), DateTime.Now);

        if (propertyType == typeof(bool))
            return faker.Random.Bool();

        if (propertyType == typeof(Guid))
            return Guid.NewGuid();

        if (propertyType == typeof(long))
            return faker.Random.Long(1, 1000);

        if (propertyType == typeof(float))
            return (float)faker.Random.Double(1, 1000);

        if (propertyType == typeof(byte))
            return (byte)faker.Random.Int(0, 255);

        if (propertyType == typeof(short))
            return (short)faker.Random.Int(-32768, 32767);

        if (propertyType.IsEnum)
        {
            var values = Enum.GetValues(propertyType);
            return values.GetValue(_random.Next(values.Length));
        }

        if (Nullable.GetUnderlyingType(propertyType) != null)
        {
            return faker.Random.Bool()
                ? null
                : GenerateValueForProperty(Nullable.GetUnderlyingType(propertyType), faker);
        }

        return null;
    }
}