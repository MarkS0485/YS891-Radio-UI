using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YS891.RadioUI.Services;

namespace YS891.RadioUI.Controls
{
    /// <summary>
    /// Band heatmap, ported from the Demo's HeatmapScreen: each sweep pass paints
    /// one row (frequency across, time down) so duty-cycled stations show up as
    /// dashed vertical stripes. A max-hold trace across the top remembers the
    /// strongest reading ever seen in each bin.
    /// </summary>
    public partial class HeatmapView
    {
        private const int Cols = 120;
        private const int Rows = 100;
        private const double PlotW = 480, PlotH = 60;

        private readonly WriteableBitmap _bitmap;
        private readonly int[] _pixels = new int[Cols * Rows];
        private readonly int[] _row = new int[Cols];
        private readonly double[] _maxHold = new double[Cols];
        private readonly double[] _current = new double[Cols];

        private long _low;
        private long _high;

        public HeatmapView()
        {
            InitializeComponent();
            _bitmap = new WriteableBitmap(Cols, Rows, 96, 96, PixelFormats.Bgra32, null);
            Bitmap.Source = _bitmap;
            Reset(14_200_000, 14_300_000);
        }

        public void Reset(long lowHz, long highHz)
        {
            _low = lowHz;
            _high = Math.Max(lowHz + 1, highHz);
            Array.Clear(_maxHold, 0, _maxHold.Length);
            Array.Clear(_current, 0, _current.Length);
            Array.Clear(_row, 0, _row.Length);
            int background = Colormap(0);
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = background;
            Flush();
            LowLabel.Text = FT891.Core.FrequencyFormat.ToMegahertzString(lowHz, 3);
            HighLabel.Text = FT891.Core.FrequencyFormat.ToMegahertzString(highHz, 3);
            RedrawTraces();
        }

        /// <summary>Feed one sweep point into the row being built.</summary>
        internal void AddPoint(SweepPoint point)
        {
            int x = (int)((double)(point.Hz - _low) / (_high - _low) * (Cols - 1));
            if (x < 0 || x >= Cols) return;
            double frac = point.Raw / 255.0;
            _row[x] = Math.Max(_row[x], point.Raw);
            _current[x] = frac;
            _maxHold[x] = Math.Max(_maxHold[x], frac);
            RedrawTraces();
        }

        /// <summary>A sweep pass finished — commit the row and scroll down.</summary>
        public void CommitPass()
        {
            Array.Copy(_pixels, 0, _pixels, Cols, Cols * (Rows - 1));
            for (int x = 0; x < Cols; x++)
            {
                _pixels[x] = Colormap(_row[x] / 255.0);
                _row[x] = 0;
                _current[x] = 0;
            }
            Flush();
        }

        private void RedrawTraces()
        {
            MaxHold.Points = TraceOf(_maxHold);
            CurrentPass.Points = TraceOf(_current);
        }

        private static PointCollection TraceOf(double[] values)
        {
            var points = new PointCollection(values.Length);
            for (int x = 0; x < values.Length; x++)
                points.Add(new Point((double)x / (values.Length - 1) * PlotW,
                                     PlotH - 3 - values[x] * (PlotH - 8)));
            return points;
        }

        private void Flush()
            => _bitmap.WritePixels(new Int32Rect(0, 0, Cols, Rows), _pixels, Cols * 4, 0);

        /// <summary>Same colormap as the audio waterfall: blue → cyan → green → yellow → red.</summary>
        private static int Colormap(double v)
        {
            byte r, g, b;
            if (v < 0.2) { double t = v / 0.2; r = 0; g = (byte)(t * 60); b = (byte)(40 + t * 160); }
            else if (v < 0.4) { double t = (v - 0.2) / 0.2; r = 0; g = (byte)(60 + t * 160); b = (byte)(200 - t * 120); }
            else if (v < 0.6) { double t = (v - 0.4) / 0.2; r = (byte)(t * 220); g = 220; b = (byte)(80 - t * 80); }
            else if (v < 0.8) { double t = (v - 0.6) / 0.2; r = 255; g = (byte)(220 - t * 160); b = 0; }
            else { double t = (v - 0.8) / 0.2; r = 255; g = (byte)(60 + t * 195); b = (byte)(t * 255); }
            return unchecked((255 << 24) | (r << 16) | (g << 8) | b);
        }
    }
}
