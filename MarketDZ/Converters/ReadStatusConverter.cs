// Converters/ReadStatusConverter.cs
using System.Globalization;
namespace MarketDZ
{

    public class ReadStatusConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isRead)
            {
                return isRead ? Colors.LightGray : Colors.White;
            }
            return Colors.White;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converters/ReadStatusFontConverter.cs
    public class ReadStatusFontConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isRead)
            {
                return isRead ? FontAttributes.None : FontAttributes.Bold;
            }
            return FontAttributes.None;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
