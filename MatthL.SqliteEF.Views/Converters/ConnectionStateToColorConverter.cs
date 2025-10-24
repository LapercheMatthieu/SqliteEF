using MatthL.SqliteEF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace MatthL.SqliteEF.Views.Converters
{
    public class ConnectionStateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConnectionState state)
            {
                return state switch
                {
                    ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    ConnectionState.Connecting => new SolidColorBrush(Color.FromRgb(255, 152, 0)), // Orange
                    ConnectionState.Disconnected => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // Gray
                    ConnectionState.Corrupted => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
                    ConnectionState.Disposed => new SolidColorBrush(Color.FromRgb(96, 125, 139)), // Blue Gray
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
