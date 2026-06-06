using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FT891.Core;

namespace YS891.RadioUI.Views
{
    /// <summary>
    /// Every meter the radio exposes, polled live, plus the status/identity reads
    /// (RadioId, IF frame, opposite VFO) and VFO B access.
    /// </summary>
    public partial class MetersPanel : UserControl
    {
        private sealed class MeterRow
        {
            public MeterType Type;
            public Rectangle Bar;
            public TextBlock Value;
        }

        private readonly List<MeterRow> _meters = new List<MeterRow>();
        private readonly DispatcherTimer _timer;
        private ICatInterface _cat;
        private Action<string> _report = m => { };
        private bool _polling;

        public MetersPanel()
        {
            InitializeComponent();
            IsEnabled = false;

            foreach (MeterType type in Enum.GetValues(typeof(MeterType)))
            {
                var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

                var label = new TextBlock
                {
                    Text = type.ToString().ToUpperInvariant(),
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = (Brush)FindResource("LcdDimTextBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(label, 0);

                var track = new Border
                {
                    Background = (Brush)FindResource("IndicatorOffBrush"),
                    CornerRadius = new CornerRadius(2),
                    Height = 14,
                };
                var bar = new Rectangle
                {
                    Fill = (Brush)FindResource("MeterGreenBrush"),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 0,
                    RadiusX = 2,
                    RadiusY = 2,
                };
                track.Child = bar;
                Grid.SetColumn(track, 1);

                var value = new TextBlock
                {
                    Text = "0",
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = (Brush)FindResource("LcdAccentBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };
                Grid.SetColumn(value, 2);

                grid.Children.Add(label);
                grid.Children.Add(track);
                grid.Children.Add(value);
                MeterRows.Children.Add(grid);
                _meters.Add(new MeterRow { Type = type, Bar = bar, Value = value });
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += async (s, e) => await PollAsync();
        }

        internal void Attach(ICatInterface cat, Action<string> report)
        {
            _cat = cat;
            _report = report ?? (m => { });
            IsEnabled = true;
        }

        internal void Detach()
        {
            _cat = null;
            LiveToggle.IsChecked = false;
            _timer.Stop();
            IsEnabled = false;
        }

        private void OnLiveToggle(object s, RoutedEventArgs e)
        {
            if (LiveToggle.IsChecked == true && _cat != null) _timer.Start();
            else _timer.Stop();
        }

        private async Task PollAsync()
        {
            var cat = _cat;
            if (cat == null || _polling) return;
            _polling = true;
            try
            {
                foreach (var meter in _meters)
                {
                    var reading = await cat.ReadMeterAsync(meter.Type, CancellationToken.None);
                    double frac = Math.Max(0, Math.Min(1, reading.Value / 255.0));
                    meter.Bar.Width = frac * Math.Max(0, ((FrameworkElement)meter.Bar.Parent).ActualWidth);
                    meter.Value.Text = FormatValue(meter.Type, reading.Value);
                }
                bool busy = await cat.GetBusyAsync(CancellationToken.None);
                BusyText.Text = busy ? "BUSY" : "";
            }
            catch (FT891Exception ex)
            {
                _timer.Stop();
                LiveToggle.IsChecked = false;
                _report($"Meters: {ex.Message}");
            }
            finally
            {
                _polling = false;
            }
        }

        private static string FormatValue(MeterType type, int raw)
        {
            switch (type)
            {
                case MeterType.Signal:
                case MeterType.Center:
                    return MeterScale.FormatSMeter(raw);
                case MeterType.SWR:
                    return $"{MeterScale.ToSwr(raw):0.0}:1";
                case MeterType.Power:
                    return $"{MeterScale.ToWatts(raw):0} W";
                default:
                    return raw.ToString();
            }
        }

        private async void OnReadStatus(object s, RoutedEventArgs e)
        {
            var cat = _cat;
            if (cat == null) return;
            try
            {
                string id = await cat.GetRadioIdAsync(CancellationToken.None);
                var info = await cat.GetRadioInfoAsync(CancellationToken.None);
                var opposite = await cat.GetOppositeVfoInfoAsync(CancellationToken.None);
                long vfoB = await cat.GetVfoBFrequencyAsync(CancellationToken.None);
                bool tx = await cat.IsTransmittingAsync(CancellationToken.None);

                StatusText.Text =
                    $"Radio ID : {id}\n" +
                    $"VFO A    : {FrequencyFormat.ToFormattedString(info.FrequencyHz)} {info.Mode}\n" +
                    $"VFO B    : {FrequencyFormat.ToFormattedString(vfoB)}\n" +
                    $"Opposite : {FrequencyFormat.ToFormattedString(opposite.FrequencyHz)} {opposite.Mode}\n" +
                    $"Channel  : {info.MemoryChannel}\n" +
                    $"TX       : {(tx ? "transmitting" : "receive")}   Split: {(info.SplitActive ? "on" : "off")}";
                VfoBBox.Text = FrequencyFormat.ToFormattedString(vfoB);
            }
            catch (FT891Exception ex)
            {
                _report($"Status: {ex.Message}");
            }
        }

        private async void OnSetVfoB(object s, RoutedEventArgs e)
        {
            var cat = _cat;
            if (cat == null) return;
            if (!FrequencyFormat.TryParseFrequency(VfoBBox.Text, out long hz))
            {
                _report($"Couldn't parse \"{VfoBBox.Text}\" as a frequency.");
                return;
            }
            try
            {
                await cat.SetVfoBFrequencyAsync(hz, CancellationToken.None);
                _report($"VFO B set to {FrequencyFormat.ToFormattedString(hz)}.");
            }
            catch (FT891Exception ex)
            {
                _report($"VFO B: {ex.Message}");
            }
        }
    }
}
