using MahApps.Metro.IconPacks;
using MatthL.SqliteEF.Databases.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace MatthL.SqliteEF.Views.Databases.SmallViews
{
    public class DatabaseStatusVisualizer
    {
        private readonly object _lockObject = new();
        private CancellationTokenSource _currentDelayCts;
        private DatabaseWorkingStatus _lastStatus;
        private readonly TimeSpan _minimumDisplayTime = TimeSpan.FromMilliseconds(500);

        public async Task UpdateStatus(DatabaseWorkingStatus newStatus, Action<PackIconMaterial> updateUI)
        {
            lock (_lockObject)
            {
                _currentDelayCts?.Cancel();
                _currentDelayCts = new CancellationTokenSource();
            }

            var icon = CreateIconForStatus(newStatus);
            updateUI(icon);

            // Si c'est un état final qui suit un état transitoire
            if (IsTerminalState(newStatus) && IsTransitionalState(_lastStatus))
            {
                try
                {
                    await Task.Delay(_minimumDisplayTime, _currentDelayCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Un nouveau changement d'état a annulé le délai
                    return;
                }

                // Vérifier si l'état n'a pas changé pendant le délai
                if (!_currentDelayCts.Token.IsCancellationRequested)
                {
                    updateUI(CreateIconForStatus(newStatus));
                }
            }

            _lastStatus = newStatus;
        }

        private bool IsTransitionalState(DatabaseWorkingStatus status) =>
            status is DatabaseWorkingStatus.Reading
                or DatabaseWorkingStatus.Writing
                or DatabaseWorkingStatus.Sending;

        private bool IsTerminalState(DatabaseWorkingStatus status) =>
            status is DatabaseWorkingStatus.Still
                or DatabaseWorkingStatus.Read
                or DatabaseWorkingStatus.Sent
                or DatabaseWorkingStatus.Written
            or DatabaseWorkingStatus.ConnectionProblem
            or DatabaseWorkingStatus.NotExisting
            or DatabaseWorkingStatus.UpdateProblem;

        public virtual PackIconMaterial CreateIconForStatus(DatabaseWorkingStatus status)
        {
            return status switch
            {
                DatabaseWorkingStatus.Writing => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseEditOutline,
                    Foreground = Brushes.Green
                },
                DatabaseWorkingStatus.Sending => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseArrowRightOutline,
                    Foreground = Brushes.Green
                },
                // ... autres cas
                _ => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseOutline,
                    Foreground = Brushes.White
                }
            };
        }
    }
    public class LocalDatabaseStatusVisualizer : DatabaseStatusVisualizer
    {
        public override PackIconMaterial CreateIconForStatus(DatabaseWorkingStatus status)
        {
            return status switch
            {
                DatabaseWorkingStatus.Writing => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseEditOutline,
                    Foreground = Brushes.Green
                },
                DatabaseWorkingStatus.Sending => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseArrowRightOutline,
                    Foreground = Brushes.Green
                },
                DatabaseWorkingStatus.Reading => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseEyeOutline,
                    Foreground = Brushes.Green
                },
                DatabaseWorkingStatus.ConnectionProblem => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseAlertOutline,
                    Foreground = Brushes.Green
                },
                DatabaseWorkingStatus.NotExisting => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseOffOutline,
                    Foreground = Brushes.Green
                },
                DatabaseWorkingStatus.UpdateProblem => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseOffOutline,
                    Foreground = Brushes.Green
                },
                // ... autres cas
                _ => new PackIconMaterial
                {
                    Kind = PackIconMaterialKind.DatabaseOutline,
                    Foreground = Brushes.White
                }
            };

            
        }

    }
}
