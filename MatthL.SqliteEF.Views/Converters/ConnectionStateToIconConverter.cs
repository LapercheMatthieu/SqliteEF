using MahApps.Metro.IconPacks;
using MatthL.SqliteEF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace MatthL.SqliteEF.Views.Converters
{
    public class ConnectionStateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConnectionState state)
            {
                return state switch
                {
                    ConnectionState.Connected => PackIconMaterialKind.DatabaseCheck,
                    ConnectionState.Connecting => PackIconMaterialKind.DatabaseSync,
                    ConnectionState.Disconnected => PackIconMaterialKind.DatabaseOff,
                    ConnectionState.Corrupted => PackIconMaterialKind.DatabaseAlert,
                    ConnectionState.Disposed => PackIconMaterialKind.DatabaseRemove,
                    _ => PackIconMaterialKind.Database
                };
            }
            return PackIconMaterialKind.Database;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
