# 🗄️ SQLite Manager - Vues WPF

## 📁 Structure des fichiers

```
Views/
├── Converters/
│   ├── BoolToVisibilityConverter.cs
│   ├── ConnectionStateToColorConverter.cs
│   ├── ConnectionStateToIconConverter.cs
│   └── HealthStatusToColorConverter.cs
│
├── CompactView/
│   ├── DatabaseCompactView.xaml
│   ├── DatabaseCompactView.xaml.cs
│   └── DatabaseCompactViewModel.cs
│
├── GeneralView/
│   ├── DatabaseGeneralView.xaml
│   ├── DatabaseGeneralView.xaml.cs
│   └── DatabaseGeneralViewModel.cs
│
└── DetailView/
    ├── DatabaseDetailView.xaml
    ├── DatabaseDetailView.xaml.cs
    └── DatabaseDetailViewModel.cs
```

## 🎯 Description des vues

### 1️⃣ Vue Compacte (DatabaseCompactView)
**Taille recommandée:** 60px de hauteur x 300px de largeur

**Caractéristiques:**
- Affichage minimaliste avec icône et nom de la base
- Indicateur de couleur selon l'état de connexion
- Indicateurs animés pour les lectures/écritures en cours
- Badge avec le nombre de readers actifs
- Indicateur de santé (petit cercle coloré)
- Clic pour ouvrir la vue détaillée

**Utilisation:**
```csharp
var viewModel = new DatabaseCompactViewModel(sqlManager);
var view = new DatabaseCompactView(viewModel);

// S'abonner à l'événement pour ouvrir les détails
viewModel.DetailsRequested += (s, e) => {
    // Ouvrir la vue détaillée
};
```

### 2️⃣ Vue Générale (DatabaseGeneralView)
**Taille recommandée:** 450px de hauteur x 400px de largeur

**Caractéristiques:**
- En-tête coloré selon l'état de connexion
- Affichage des informations principales (chemin, taille, dates)
- Message d'alerte quand la DB n'est pas connectée
- Boutons d'actions: Sélectionner, Créer, Supprimer, Détails
- Bouton pour ouvrir le dossier dans l'explorateur
- Gestion des dialogues de fichiers

**Utilisation:**
```csharp
var viewModel = new DatabaseGeneralViewModel(sqlManager);
var view = new DatabaseGeneralView(viewModel);

// S'abonner à l'événement pour ouvrir les détails
viewModel.DetailsRequested += (s, e) => {
    // Ouvrir la vue détaillée
};
```

### 3️⃣ Vue Détaillée (DatabaseDetailView)
**Taille recommandée:** 700px+ de hauteur x 650px de largeur

**Caractéristiques:**
- **Carte Santé:** Statut, temps de réponse, vérification d'intégrité
- **Carte Statistiques:** Utilisation de l'espace, nombre de tables, pages
- **Carte Configuration:** Paramètres SQLite (WAL, cache, synchronous, etc.)
- **Carte Fichiers:** Taille des fichiers .db, .db-wal, .db-shm
- Actualisation automatique toutes les 5 secondes
- Boutons: Actualiser, Synchroniser (Flush), Ouvrir dossier
- Indicateurs d'activité en temps réel (lecture/écriture)

**Utilisation:**
```csharp
var viewModel = new DatabaseDetailViewModel(sqlManager);
var view = new DatabaseDetailView(viewModel);

// Le ViewModel se rafraîchit automatiquement
// Pensez à disposer le ViewModel quand la vue est fermée
viewModel.Dispose();
```

## 🎨 Palette de couleurs

### États de connexion
- **Connected (Connecté):** `#4CAF50` (Vert)
- **Connecting (Connexion):** `#FF9800` (Orange)
- **Disconnected (Déconnecté):** `#9E9E9E` (Gris)
- **Corrupted (Corrompu):** `#F44336` (Rouge)
- **Disposed:** `#607D8B` (Gris-bleu)

### États de santé
- **Healthy (Sain):** `#4CAF50` (Vert)
- **Degraded (Dégradé):** `#FFC107` (Jaune/Ambre)
- **Unhealthy (Mauvais état):** `#F44336` (Rouge)

### Activités
- **Lecture:** `#2196F3` (Bleu)
- **Écriture:** `#FF9800` (Orange)

## 🔧 Configuration requise

### Packages NuGet nécessaires
```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.x.x" />
<PackageReference Include="MahApps.Metro.IconPacks" Version="4.x.x" />
```

### Namespaces à ajouter dans votre projet
Les vues utilisent les namespaces suivants:
- `MatthL.SqliteEF.Views.CompactView`
- `MatthL.SqliteEF.Views.GeneralView`
- `MatthL.SqliteEF.Views.DetailView`
- `MatthL.SqliteEF.Views.Converters`

Si vos namespaces sont différents, pensez à les ajuster dans les fichiers XAML et C#.

## 📝 Événements du SQLManager utilisés

Les ViewModels s'abonnent aux événements suivants du SQLManager:

```csharp
// État de connexion
sqlManager.ConnectionStateChanged += (sender, state) => { /* ... */ };

// Activité de lecture
sqlManager.ReadOperationStarted += (remaining) => { /* ... */ };
sqlManager.ReadOperationEnded += (remaining) => { /* ... */ };

// Activité d'écriture
sqlManager.WriteOperationStarted += () => { /* ... */ };
sqlManager.WriteOperationEnded += () => { /* ... */ };
```

## 🎭 Converters disponibles

### `ConnectionStateToColorConverter`
Convertit un `ConnectionState` en `SolidColorBrush`

### `ConnectionStateToIconConverter`
Convertit un `ConnectionState` en `PackIconMaterialKind`

### `HealthStatusToColorConverter`
Convertit un `HealthStatus` en `SolidColorBrush`

### `BoolToVisibilityConverter`
Convertit un `bool` en `Visibility`

### `InverseBoolToVisibilityConverter`
Convertit un `bool` en `Visibility` (inversé)

## ⚡ Propriétés du SQLManager requises

Les vues utilisent les propriétés/méthodes suivantes du SQLManager:

### Propriétés
- `IsConnected` (bool)
- `CurrentState` (ConnectionState)
- `GetFileName` (string)
- `GetFolderPath` (string)
- `GetFullPath` (string)
- `GetFileExtension` (string)
- `GetFileSize` (long)
- `IsInMemory` (bool)
- `LastConnection` (DateTime?)
- `LastActivity` (DateTime?)
- `IsReading` (bool)
- `IsWriting` (bool)
- `ActiveReaders` (int)

### Méthodes async
- `ConnectAsync()` → Result
- `DisconnectAsync()` → Result
- `Create()` → Result
- `DeleteCurrentDatabase()` → Result
- `CheckHealthAsync()` → HealthCheckResult
- `QuickHealthCheckAsync()` → HealthCheckResult
- `GetDatabaseStatisticsAsync()` → Result<DatabaseStatistics>
- `GetDatabaseFileInfo()` → Result<DatabaseFileInfo>
- `GetConcurrencyConfigAsync()` → Result<Dictionary<string, string>>
- `FlushAsync()` → Result

## 🚀 Exemple d'utilisation complète

```csharp
using MatthL.SqliteEF.Views.CompactView;
using MatthL.SqliteEF.Views.GeneralView;
using MatthL.SqliteEF.Views.DetailView;

public class MainWindow : Window
{
    private SQLManager _sqlManager;
    private DatabaseCompactViewModel _compactVM;
    private DatabaseGeneralViewModel _generalVM;
    private DatabaseDetailViewModel _detailVM;

    public MainWindow()
    {
        InitializeComponent();
        
        // Créer le SQLManager avec votre contexte factory
        _sqlManager = new SQLManager(contextFactory, folderPath, fileName);
        
        // Créer les ViewModels
        _compactVM = new DatabaseCompactViewModel(_sqlManager);
        _generalVM = new DatabaseGeneralViewModel(_sqlManager);
        _detailVM = new DatabaseDetailViewModel(_sqlManager);
        
        // Créer les vues
        var compactView = new DatabaseCompactView(_compactVM);
        var generalView = new DatabaseGeneralView(_generalVM);
        var detailView = new DatabaseDetailView(_detailVM);
        
        // S'abonner aux événements pour naviguer entre les vues
        _compactVM.DetailsRequested += (s, e) => ShowDetailView();
        _generalVM.DetailsRequested += (s, e) => ShowDetailView();
        
        // Ajouter les vues à votre interface
        CompactViewContainer.Content = compactView;
        GeneralViewContainer.Content = generalView;
        DetailViewContainer.Content = detailView;
    }
    
    private void ShowDetailView()
    {
        // Logique pour afficher la vue détaillée
        // (popup, navigation, changement de contenu, etc.)
    }
    
    protected override void OnClosed(EventArgs e)
    {
        // Important: Disposer les ViewModels
        _compactVM?.Dispose();
        _generalVM?.Dispose();
        _detailVM?.Dispose();
        
        base.OnClosed(e);
    }
}
```

## 🎨 Personnalisation

### Modifier les couleurs
Les couleurs sont définies dans les converters. Modifiez les valeurs RGB dans:
- `ConnectionStateToColorConverter.cs`
- `HealthStatusToColorConverter.cs`

### Modifier les icônes
Les icônes sont définies dans `ConnectionStateToIconConverter.cs` et directement dans les XAML.
Consultez la documentation de MahApps.Metro.IconPacks pour voir toutes les icônes disponibles.

### Modifier les durées de rafraîchissement
Dans `DatabaseCompactViewModel.cs` et `DatabaseDetailViewModel.cs`:
```csharp
// Pour la vue compacte (ligne ~48)
_updateTimer.Interval = TimeSpan.FromSeconds(5); // Modifier ici

// Pour la vue détaillée (ligne ~165)
_refreshTimer.Interval = TimeSpan.FromSeconds(5); // Modifier ici
```

## 🐛 Troubleshooting

### Les icônes ne s'affichent pas
→ Vérifiez que le package `MahApps.Metro.IconPacks.Material` est installé

### Les animations ne fonctionnent pas
→ Assurez-vous que les Triggers XAML sont correctement configurés dans votre application

### Les événements ne se déclenchent pas
→ Vérifiez que votre `SQLManager` a bien implémenté les événements:
   - `ReadOperationStarted`
   - `ReadOperationEnded`
   - `WriteOperationStarted`
   - `WriteOperationEnded`
   - `ConnectionStateChanged`

### Memory leak après fermeture
→ Pensez à appeler `.Dispose()` sur tous les ViewModels quand vous fermez les vues

## 📦 Fichiers à intégrer dans votre projet

1. Copiez tous les fichiers dans votre projet WPF
2. Ajustez les namespaces si nécessaire
3. Assurez-vous que les packages NuGet sont installés
4. Compilez et testez !

## 💡 Conseils d'utilisation

- **Vue Compacte:** Idéale pour une liste de bases de données ou un menu latéral
- **Vue Générale:** Parfaite pour un dashboard ou un écran de gestion
- **Vue Détaillée:** À utiliser dans une fenêtre modale ou un écran dédié au monitoring

## 📄 Licence

Ces vues sont conçues pour être utilisées avec le SQLManager de MatthL.
Modifiez-les librement selon vos besoins !

---

**Créé avec ❤️ pour faciliter la gestion de bases SQLite dans vos applications WPF**
