# 🎮 SQLite Manager Demo

## 📋 Description

Application de démonstration WPF pour tester les 3 vues du SQLite Manager:
- **Vue Compacte** (gauche, haut): Affichage minimaliste avec indicateurs d'activité
- **Vue Générale** (gauche, bas): Carte complète avec actions
- **Vue Détaillée** (droite): Statistiques, santé, configuration complète

## 🚀 Lancement rapide

### Option 1: Avec Visual Studio
1. Ouvrez `MatthL.SqliteEF.Demo.csproj` dans Visual Studio
2. Restaurez les packages NuGet (Build > Restore NuGet Packages)
3. Compilez et exécutez (F5)

### Option 2: Avec .NET CLI
```bash
cd Demo
dotnet restore
dotnet run
```

## 📦 Structure du projet

```
Demo/
├── App.xaml                          # Point d'entrée de l'application
├── App.xaml.cs
├── MainWindow.xaml                   # Fenêtre principale avec les 3 vues
├── MainWindow.xaml.cs
├── MockSQLManager.cs                 # Mock du SQLManager pour la démo
└── MatthL.SqliteEF.Demo.csproj      # Fichier de projet
```

## 🎯 Fonctionnalités de la démo

### Données mockées
La démo crée 3 bases de données fictives:
1. **users.db** - Connectée et active
2. **products.sqlite** - Connectée et active
3. **orders.db** - Déconnectée

### Simulations
Le `MockSQLManager` simule:
- ✅ Changements d'état de connexion
- ✅ Opérations de lecture aléatoires (avec indicateur animé)
- ✅ Opérations d'écriture aléatoires (avec indicateur animé)
- ✅ Health checks avec résultats réalistes
- ✅ Statistiques de base de données
- ✅ Informations de fichiers (WAL, SHM)
- ✅ Configuration SQLite

### Interactions disponibles
- 🖱️ Clic sur une vue compacte → Met à jour les vues générale et détaillée
- 📊 Les indicateurs de lecture/écriture s'animent en temps réel
- 🔄 Les statistiques se rafraîchissent automatiquement
- 🟢 Les indicateurs de santé changent de couleur selon l'état

## 🔧 Configuration

### Packages NuGet requis
- `CommunityToolkit.Mvvm` (8.2.2+)
- `MahApps.Metro.IconPacks.Material` (4.11.0+)
- `Microsoft.EntityFrameworkCore` (8.0.0+)
- `Microsoft.EntityFrameworkCore.Sqlite` (8.0.0+)

### Framework cible
- .NET 8.0 Windows

## 📝 Notes importantes

### MockSQLManager
Le `MockSQLManager` hérite de `SQLManager` et override les propriétés/méthodes pour retourner des données fictives. Il utilise la réflexion pour déclencher les événements privés.

**⚠️ Important:** Ce mock est **uniquement pour la démo**. Dans votre application réelle, utilisez votre vrai `SQLManager` avec un vrai contexte EF Core.

### Intégration dans votre projet

Pour utiliser les vues dans votre vrai projet:

1. **Remplacez** `MockSQLManager` par votre vrai `SQLManager`
2. **Fournissez** une vraie factory de contexte:
```csharp
Func<string, RootDbContext> contextFactory = (path) => new YourDbContext(path);
var sqlManager = new SQLManager(contextFactory, folderPath, fileName);
```

3. **Créez** les ViewModels avec votre manager:
```csharp
var compactVM = new DatabaseCompactViewModel(sqlManager);
var generalVM = new DatabaseGeneralViewModel(sqlManager);
var detailVM = new DatabaseDetailViewModel(sqlManager);
```

## 🎨 Personnalisation

### Modifier les bases de données mockées
Dans `MainWindow.xaml.cs`, méthode `InitializeDemo()`:
```csharp
_mockManagers = new List<MockSQLManager>
{
    MockSQLManager.CreateConnectedDatabase("votreDb", @"C:\VotreChemin", ".db"),
    // ... ajoutez d'autres bases
};
```

### Modifier la mise en page
Éditez `MainWindow.xaml` pour changer l'agencement des vues.

### Modifier les intervalles de rafraîchissement
Dans les ViewModels:
- `DatabaseCompactViewModel`: Ligne ~48 (5 secondes)
- `DatabaseDetailViewModel`: Ligne ~165 (5 secondes)

## 🐛 Dépannage

### Les vues ne s'affichent pas
→ Vérifiez que les namespaces dans les fichiers XAML correspondent à votre structure de projet

### Les icônes ne s'affichent pas
→ Assurez-vous que `MahApps.Metro.IconPacks.Material` est bien installé

### Les animations ne fonctionnent pas
→ Vérifiez que les événements du MockSQLManager se déclenchent correctement

### Erreurs de compilation
→ Vérifiez que toutes les références de projet sont correctes dans le .csproj

## 📚 Ressources

- [Documentation CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [MahApps.Metro IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)

## 🎉 Amusez-vous bien !

Cette démo vous permet de voir toutes les fonctionnalités des vues en action sans avoir besoin de configurer une vraie base de données. Parfait pour tester l'UI et comprendre comment tout fonctionne ensemble !

---

**Note:** N'oubliez pas de copier les fichiers des vues (CompactView, GeneralView, DetailView, Converters) dans votre projet pour que la démo fonctionne !
