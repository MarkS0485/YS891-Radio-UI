using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace YS891.RadioUI.Controls
{
    /// <summary>
    /// Disco mode, vintage 1986: flat-colour lights spawned at random, a strobing
    /// wash, a light-up dance floor and a scrolling marquee. No gradients, no
    /// easing, no taste — that's the point. A crude energy-spike beat detector
    /// makes everything slam on the beat when audio capture is running, and the
    /// DJ booth plays any MP3/WAV through the speakers (start loopback capture
    /// in AUDIO LAB and the lights dance to the track).
    /// </summary>
    public partial class DiscoOverlay
    {
        private static readonly Brush[] Neon =
        {
            Brushes.Magenta, Brushes.Cyan, Brushes.Yellow, Brushes.Lime,
            Brushes.OrangeRed, Brushes.HotPink, Brushes.DeepSkyBlue,
        };

        private readonly DispatcherTimer _lightsTimer;
        private readonly DispatcherTimer _marqueeTimer;
        private readonly Random _random = new Random();
        private readonly Border[] _floorTiles;
        private readonly Queue<double> _energyHistory = new Queue<double>();
        private readonly MediaPlayer _player = new MediaPlayer();
        private readonly Stopwatch _beatClock = Stopwatch.StartNew();

        private double _level = 0.3;
        private double _beatPulse;
        private long _lastBeatMs;
        private double _marqueeX;
        private int _beat;
        private bool _trackLoaded;

        /// <summary>Raised when a track starts — the host should start loopback capture.</summary>
        public event Action TrackStarted;

        public DiscoOverlay()
        {
            InitializeComponent();

            _floorTiles = new Border[28];
            for (int i = 0; i < _floorTiles.Length; i++)
            {
                _floorTiles[i] = new Border
                {
                    Background = Neon[_random.Next(Neon.Length)],
                    Margin = new Thickness(2),
                };
                Floor.Children.Add(_floorTiles[i]);
            }

            _player.MediaEnded += (s, e) => { _player.Position = TimeSpan.Zero; _player.Play(); }; // loop the night away

            _lightsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            _lightsTimer.Tick += (s, e) => SpawnLights();
            _marqueeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _marqueeTimer.Tick += (s, e) => ScrollMarquee();
            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    _marqueeX = ActualWidth;
                    _lightsTimer.Start();
                    _marqueeTimer.Start();
                    if (_trackLoaded) _player.Play();
                }
                else
                {
                    _lightsTimer.Stop();
                    _marqueeTimer.Stop();
                    Lights.Children.Clear();
                    _player.Pause(); // the party resumes where it left off
                }
            };
        }

        /// <summary>
        /// Audio RMS 0–1 from the analyzer (~20×/s). Drives the light intensity and
        /// the beat detector: a spike well above the rolling average is a beat.
        /// </summary>
        public void SetLevel(double level)
        {
            _level = Math.Max(0.15, Math.Min(1.0, level * 6));

            double avg = 0;
            foreach (double e in _energyHistory) avg += e;
            if (_energyHistory.Count > 0) avg /= _energyHistory.Count;
            _energyHistory.Enqueue(level);
            while (_energyHistory.Count > 40) _energyHistory.Dequeue();

            long now = _beatClock.ElapsedMilliseconds;
            if (level > 0.04 && level > avg * 1.5 && now - _lastBeatMs > 220)
            {
                _lastBeatMs = now;
                OnBeat();
            }
        }

        private void OnBeat()
        {
            if (Visibility != Visibility.Visible) return;
            _beatPulse = 1.0;

            // Slam the floor: every tile a fresh colour, RIGHT NOW.
            foreach (var tile in _floorTiles)
                tile.Background = Neon[_random.Next(Neon.Length)];

            // Hard strobe pop.
            Strobe.Background = Brushes.White;
            Strobe.Opacity = 0.30;

            SpawnLights();
        }

        private void SpawnLights()
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 10 || h < 10) return;

            Lights.Children.Clear();
            _beat++;
            _beatPulse *= 0.8; // decay between beats

            double punch = Math.Min(1.5, _level + _beatPulse);
            int count = 8 + (int)(punch * 16);
            for (int i = 0; i < count; i++)
            {
                double size = (20 + _random.NextDouble() * 70) * (0.5 + punch);
                Shape light = _random.Next(3) == 0
                    ? new Rectangle { Width = size, Height = size }
                    : (Shape)new Ellipse { Width = size, Height = size };
                light.Fill = Neon[_random.Next(Neon.Length)];
                light.Opacity = 0.55 + _random.NextDouble() * 0.45;
                Canvas.SetLeft(light, _random.NextDouble() * w - size / 2);
                Canvas.SetTop(light, _random.NextDouble() * (h - 120) - size / 2);
                Lights.Children.Add(light);
            }

            // Crude strobe wash between beats: alternate hard colours. Subtle? No.
            if (_beatPulse < 0.4)
            {
                Strobe.Background = _beat % 2 == 0 ? Neon[_random.Next(Neon.Length)] : Brushes.Black;
                Strobe.Opacity = 0.18;
            }

            // The floor shuffles a few tiles every tick even without a beat.
            for (int i = 0; i < 4; i++)
                _floorTiles[_random.Next(_floorTiles.Length)].Background = Neon[_random.Next(Neon.Length)];
        }

        private void ScrollMarquee()
        {
            _marqueeX -= 3;
            if (_marqueeX < -Marquee.ActualWidth) _marqueeX = ActualWidth;
            Canvas.SetLeft(Marquee, _marqueeX);
        }

        // ----- DJ booth ---------------------------------------------------------

        private void OnLoadTrack(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Pick tonight's track",
                Filter = "Audio (*.mp3;*.wav;*.wma)|*.mp3;*.wav;*.wma|All files (*.*)|*.*",
            };
            if (dialog.ShowDialog() != true) return;

            _player.Open(new Uri(dialog.FileName));
            _player.Play();
            _trackLoaded = true;
            NowPlaying.Text = $"♫ {System.IO.Path.GetFileNameWithoutExtension(dialog.FileName).ToUpperInvariant()}";
            StopTrack.Visibility = Visibility.Visible;
            TrackStarted?.Invoke(); // host starts loopback capture → lights hear the music
        }

        private void OnStopTrack(object sender, RoutedEventArgs e)
        {
            _player.Stop();
            _trackLoaded = false;
            NowPlaying.Text = "";
            StopTrack.Visibility = Visibility.Collapsed;
        }

        private void OnHitSingle(object sender, RoutedEventArgs e)
        {
            // You know the rules, and so do I.
            Process.Start(new ProcessStartInfo("https://www.youtube.com/watch?v=dQw4w9WgXcQ") { UseShellExecute = true });
        }
    }
}
