using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using FT891.Core;

namespace YS891.RadioUI.Controls
{
    /// <summary>
    /// Segmented S-meter: lit segments track the raw 0–255 reading with a
    /// green→amber→red ramp, labelled via <see cref="MeterScale.FormatSMeter"/>.
    /// </summary>
    public partial class SMeterBar
    {
        private const int SegmentCount = 24;

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(SMeterBar),
            new PropertyMetadata(0, (d, e) => ((SMeterBar)d).Render()));

        private readonly Rectangle[] _segments = new Rectangle[SegmentCount];
        private readonly Brush _offBrush;
        private readonly Brush _greenBrush;
        private readonly Brush _amberBrush;
        private readonly Brush _redBrush;

        public SMeterBar()
        {
            InitializeComponent();
            _offBrush = (Brush)FindResource("IndicatorOffBrush");
            _greenBrush = (Brush)FindResource("MeterGreenBrush");
            _amberBrush = (Brush)FindResource("MeterAmberBrush");
            _redBrush = (Brush)FindResource("MeterRedBrush");

            for (int i = 0; i < SegmentCount; i++)
            {
                _segments[i] = new Rectangle
                {
                    Margin = new Thickness(1, 0, 1, 0),
                    RadiusX = 1,
                    RadiusY = 1,
                    Fill = _offBrush,
                };
                Segments.Children.Add(_segments[i]);
            }
            Render();
        }

        /// <summary>Raw S-meter reading, 0–255.</summary>
        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private void Render()
        {
            int raw = Value;
            int lit = (int)System.Math.Round(raw / 255.0 * SegmentCount);
            for (int i = 0; i < SegmentCount; i++)
            {
                _segments[i].Fill = i >= lit ? _offBrush
                    : i < SegmentCount * 6 / 10 ? _greenBrush
                    : i < SegmentCount * 8 / 10 ? _amberBrush
                    : _redBrush;
            }
            Label.Text = MeterScale.FormatSMeter(raw);
        }
    }
}
