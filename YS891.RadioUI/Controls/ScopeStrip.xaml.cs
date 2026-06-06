using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FT891.Core;
using YS891.RadioUI.Services;

namespace YS891.RadioUI.Controls
{
    /// <summary>
    /// The display's graph area. Default mode is a scrolling signal-strength-over-
    /// time trace fed by the live S-meter (never interrupts receive). During a
    /// sweep it switches to a spectrum across the swept span, holds the result for
    /// a few seconds afterwards, then falls back to the live trace.
    /// </summary>
    public partial class ScopeStrip
    {
        private const int HistoryLength = 160;
        private const double PlotWidth = 480;
        private const double PlotHeight = 120;

        private readonly Queue<int> _history = new Queue<int>(HistoryLength);
        private readonly List<SweepPoint> _sweepPoints = new List<SweepPoint>();
        private readonly DispatcherTimer _holdTimer;

        private bool _sweepMode;
        private long _sweepLow;
        private long _sweepHigh;

        public ScopeStrip()
        {
            InitializeComponent();
            _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _holdTimer.Tick += (s, e) =>
            {
                _holdTimer.Stop();
                _sweepMode = false;
                RenderLive();
            };
        }

        /// <summary>Feed one live S-meter sample (raw 0–255).</summary>
        public void AddSample(int raw)
        {
            if (_history.Count >= HistoryLength) _history.Dequeue();
            _history.Enqueue(raw);
            if (!_sweepMode) RenderLive();
        }

        public void BeginSweep(long lowHz, long highHz)
        {
            _holdTimer.Stop();
            _sweepMode = true;
            _sweepLow = lowHz;
            _sweepHigh = highHz;
            _sweepPoints.Clear();
            ModeBadge.Text = "SWEEP";
            LowLabel.Text = FrequencyFormat.ToMegahertzString(lowHz, 3);
            HighLabel.Text = FrequencyFormat.ToMegahertzString(highHz, 3);
            RenderSweep();
        }

        internal void AddSweepPoint(SweepPoint point)
        {
            if (!_sweepMode) return;
            _sweepPoints.Add(point);
            RenderSweep();
        }

        public void EndSweep()
        {
            if (!_sweepMode) return;
            ModeBadge.Text = "SWEEP (held)";
            _holdTimer.Start(); // keep the result on screen, then fall back to live
        }

        public void Clear()
        {
            _holdTimer.Stop();
            _sweepMode = false;
            _history.Clear();
            _sweepPoints.Clear();
            RenderLive();
        }

        private static double YFor(int raw)
            => PlotHeight - 4 - raw / 255.0 * (PlotHeight - 14);

        private void RenderLive()
        {
            ModeBadge.Text = "LIVE";
            LowLabel.Text = "";
            HighLabel.Text = "";

            var points = new PointCollection();
            int i = 0;
            foreach (int raw in _history)
            {
                double x = (double)i / (HistoryLength - 1) * PlotWidth;
                points.Add(new Point(x, YFor(raw)));
                i++;
            }
            ApplyTrace(points);
        }

        private void RenderSweep()
        {
            var points = new PointCollection();
            double span = Math.Max(1, _sweepHigh - _sweepLow);
            foreach (var p in _sweepPoints)
            {
                double x = (p.Hz - _sweepLow) / span * PlotWidth;
                points.Add(new Point(x, YFor(p.Raw)));
            }
            ApplyTrace(points);
        }

        private void ApplyTrace(PointCollection points)
        {
            Trace.Points = points;

            // Filled area under the trace for that classic scope glow.
            var fill = new PointCollection();
            if (points.Count > 1)
            {
                fill.Add(new Point(points[0].X, PlotHeight));
                foreach (var p in points) fill.Add(p);
                fill.Add(new Point(points[points.Count - 1].X, PlotHeight));
            }
            TraceFill.Points = fill;
        }
    }
}
