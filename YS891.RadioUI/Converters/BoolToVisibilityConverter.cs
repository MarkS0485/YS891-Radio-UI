using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YS891.RadioUI.Converters
{
    /// <summary>True → Visible, false → the configured fallback (default Collapsed).</summary>
    internal sealed class BoolToVisibilityConverter : IValueConverter
    {
        public Visibility WhenFalse { get; set; } = Visibility.Collapsed;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : WhenFalse;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
