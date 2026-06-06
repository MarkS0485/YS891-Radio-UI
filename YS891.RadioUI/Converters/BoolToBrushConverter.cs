using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace YS891.RadioUI.Converters
{
    /// <summary>True → <see cref="OnBrush"/>, false → <see cref="OffBrush"/>.</summary>
    internal sealed class BoolToBrushConverter : IValueConverter
    {
        public Brush OnBrush { get; set; } = Brushes.OrangeRed;
        public Brush OffBrush { get; set; } = Brushes.DimGray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? OnBrush : OffBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
