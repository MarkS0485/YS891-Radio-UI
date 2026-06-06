using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YS891.RadioUI.Controls
{
    /// <summary>
    /// Scrolling audio waterfall: newest FFT line at the top, classic
    /// blue→cyan→green→yellow→red colormap, 0–5 kHz across the width.
    /// </summary>
    public partial class WaterfallView
    {
        private const int Cols = 256;
        private const int Rows = 120;
        private const double TopHz = 5_000;
        private const double BinHz = 22_050.0 / 2_048;
        private const double FloorDb = -80;

        private readonly WriteableBitmap _bitmap;
        private readonly int[] _pixels = new int[Cols * Rows];

        public WaterfallView()
        {
            InitializeComponent();
            _bitmap = new WriteableBitmap(Cols, Rows, 96, 96, PixelFormats.Bgra32, null);
            Bitmap.Source = _bitmap;
            int background = Colormap(0);
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = background;
            Flush();
        }

        public void AddFrame(double[] magnitudes)
        {
            // Scroll everything down one row.
            Array.Copy(_pixels, 0, _pixels, Cols, Cols * (Rows - 1));

            int usableBins = (int)(TopHz / BinHz);
            for (int x = 0; x < Cols; x++)
            {
                int bin = (int)((double)x / Cols * usableBins);
                double db = 20 * Math.Log10(Math.Max(1e-9, magnitudes[Math.Min(bin, magnitudes.Length - 1)]));
                double frac = Math.Max(0, Math.Min(1, (db - FloorDb) / -FloorDb));
                _pixels[x] = Colormap(frac);
            }
            Flush();
        }

        private void Flush()
            => _bitmap.WritePixels(new Int32Rect(0, 0, Cols, Rows), _pixels, Cols * 4, 0);

        /// <summary>0–1 → BGRA: deep blue, cyan, green, yellow, red, white-hot.</summary>
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
