using System.Globalization;
using Microsoft.Maui.Controls;

namespace MarketDZ.Converters
{
    /// <summary>
    /// Converter that determines the color for a star in a rating based on the rating value and star position
    /// </summary>
    public class RatingColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not int rating || parameter is not string position || !int.TryParse(position, out int starPosition))
                return Colors.LightGray;

            return rating >= starPosition ? Colors.Gold : Colors.LightGray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}