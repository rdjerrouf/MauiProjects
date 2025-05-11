using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace MarketDZ.Converters
{
    /// <summary>
    /// Converter that returns a text description based on a rating value
    /// </summary>
    public class RatingTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not int rating)
                return "Please rate";

            return rating switch
            {
                1 => "Poor",
                2 => "Below Average",
                3 => "Average",
                4 => "Good",
                5 => "Excellent",
                _ => "Please rate"
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}