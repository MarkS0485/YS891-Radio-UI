using System.Windows;
using System.Windows.Media;

namespace YS891.RadioUI.Controls
{
    /// <summary>Oscilloscope trace of the received channel audio.</summary>
    public partial class WaveformView
    {
        private const double W = 480, H = 120;

        public WaveformView()
        {
            InitializeComponent();
        }

        public void AddSamples(short[] samples)
        {
            NoAudioHint.Visibility = Visibility.Collapsed;
            var points = new PointCollection(samples.Length);
            for (int i = 0; i < samples.Length; i++)
            {
                double x = (double)i / (samples.Length - 1) * W;
                double y = H / 2 - samples[i] / 32768.0 * (H / 2 - 4);
                points.Add(new Point(x, y));
            }
            Trace.Points = points;
        }

        public void ShowIdle()
        {
            Trace.Points = new PointCollection();
            NoAudioHint.Visibility = Visibility.Visible;
        }
    }
}
