using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace YS891.RadioUI.Controls
{
    /// <summary>
    /// 1980s-style bar spectrum analyzer: green bars, amber tops, falling red
    /// peak-hold caps. Shows 0–5 kHz of the channel audio.
    /// </summary>
    public partial class SpectrumView
    {
        private const int BarCount = 48;
        private const double W = 480, H = 120;
        private const double TopHz = 5_000;
        private const double BinHz = 22_050.0 / 2_048;
        private const double FloorDb = -80;

        private readonly Rectangle[] _bars = new Rectangle[BarCount];
        private readonly Rectangle[] _caps = new Rectangle[BarCount];
        private readonly double[] _peaks = new double[BarCount];

        public SpectrumView()
        {
            InitializeComponent();
            var barBrush = (Brush)FindResource("MeterGreenBrush");
            var capBrush = (Brush)FindResource("MeterRedBrush");
            double barWidth = W / BarCount - 2;

            for (int i = 0; i < BarCount; i++)
            {
                _bars[i] = new Rectangle { Width = barWidth, Height = 0, Fill = barBrush };
                Canvas.SetLeft(_bars[i], i * W / BarCount + 1);
                Canvas.SetTop(_bars[i], H);
                Plot.Children.Add(_bars[i]);

                _caps[i] = new Rectangle { Width = barWidth, Height = 2, Fill = capBrush };
                Canvas.SetLeft(_caps[i], i * W / BarCount + 1);
                Canvas.SetTop(_caps[i], H);
                Plot.Children.Add(_caps[i]);
            }
        }

        public void AddFrame(double[] magnitudes)
        {
            int binsPerBar = (int)Math.Max(1, TopHz / BinHz / BarCount);
            for (int i = 0; i < BarCount; i++)
            {
                // Average the bins belonging to this bar, convert to dB.
                double sum = 0;
                int start = i * binsPerBar;
                for (int b = start; b < start + binsPerBar && b < magnitudes.Length; b++)
                    sum += magnitudes[b];
                double db = 20 * Math.Log10(Math.Max(1e-9, sum / binsPerBar));
                double frac = Math.Max(0, Math.Min(1, (db - FloorDb) / -FloorDb));

                double height = frac * (H - 16);
                _bars[i].Height = height;
                Canvas.SetTop(_bars[i], H - height);

                // Peak hold with slow decay.
                _peaks[i] = Math.Max(frac, _peaks[i] - 0.012);
                double capY = H - _peaks[i] * (H - 16) - 2;
                Canvas.SetTop(_caps[i], capY);
            }
        }
    }
}
