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
    public class HealthStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HealthStatus status)
            {
                return status switch
                {
                    HealthStatus.Healthy => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    HealthStatus.Degraded => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Yellow/Amber
                    HealthStatus.Unhealthy => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red
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
