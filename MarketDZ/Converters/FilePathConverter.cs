using System.Globalization;
using System.Diagnostics;
using Microsoft.Maui.Controls;

namespace MarketDZ.Converters
{
    public class FilePathConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path))
            {
                Debug.WriteLine($"Converting path: {path}");

                // If it's already a URI, return it
                if (path.StartsWith("file://") || path.StartsWith("http"))
                    return path;

                // Otherwise convert to file URI
                return $"file://{path}";
            }

            return "placeholder.png"; // Default fallback
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}