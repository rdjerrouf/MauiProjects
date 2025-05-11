// BoolToFontAttributesConverter.cs
using System.Globalization;

namespace MarketDZ.Converters
{
    public class BoolToFontAttributesConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isRead)
            {
                return isRead ? FontAttributes.None : FontAttributes.Bold;
            }
            return FontAttributes.None;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}