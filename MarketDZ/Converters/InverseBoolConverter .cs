using System.Globalization;

namespace MarketDZ.Converters
{
    /// <summary>
    /// Converts boolean values to their inverse
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to its inverse value
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }

        /// <summary>
        /// Converts back a boolean to its inverse value
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }
    }

    /// <summary>
    /// Converts string to boolean based on whether it's null or empty
    /// </summary>
    public class StringNotNullOrEmptyBoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts a string to boolean - true if string is not null or empty
        /// </summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return !string.IsNullOrEmpty(stringValue);
            }
            return false;
        }

        /// <summary>
        /// Convert back operation (not implemented)
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}