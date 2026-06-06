using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FT891.Core;
using YS891.RadioUI.Services;
using YS891.RadioUI.ViewModels;
using YS891.RadioUI.Views;

namespace YS891.RadioUI
{
    /// <summary>The full-feature demo window: front panel plus coverage tabs.</summary>
    public partial class MainWindow : Window
    {
        private readonly RadioViewModel _vm = new RadioViewModel();
        private readonly AudioSource _audio = new AudioSource();
        private readonly AudioAnalyzer _analyzer = new AudioAnalyzer();
        private bool _shutdownComplete;
        private bool _loadingBands = true;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            VfoDial.DetentChanged += _vm.OnDialDetents;
            MultiKnob.DetentChanged += _vm.OnMultiDetents;

            // Scan output goes to the LCD strip, the big scanner scope, and the heatmap.
            _vm.SMeterSample += raw => { Scope.AddSample(raw); BigScope.AddSample(raw); };
            _vm.SweepStarted += (low, high) =>
            {
                Scope.BeginSweep(low, high);
                BigScope.BeginSweep(low, high);
                Heatmap.Reset(low, high);
            };
            _vm.SweepPointReceived += p =>
            {
                Scope.AddSweepPoint(p);
                BigScope.AddSweepPoint(p);
                Heatmap.AddPoint(p);
            };
            _vm.SweepPassStarted += Heatmap.CommitPass;
            _vm.SweepEnded += () => { Scope.EndSweep(); BigScope.EndSweep(); Heatmap.CommitPass(); };

            // Audio pipeline: capture (worker thread) → dispatcher → analyzer → views.
            WaveView.ShowIdle();
            _audio.SamplesAvailable += samples => Dispatcher.BeginInvoke(new Action(() => _analyzer.Push(samples)));
            _analyzer.WaveformBlock += WaveView.AddSamples;
            _analyzer.SpectrumFrame += magnitudes =>
            {
                SpecView.AddFrame(magnitudes);
                FallView.AddFrame(magnitudes);
            };
            _analyzer.Level += Disco.SetLevel;
            Disco.TrackStarted += EnsureLoopbackCapture; // the lights need to hear the music

            foreach (var name in AudioSource.GetDeviceNames())
                AudioDeviceBox.Items.Add(name);
            AudioDeviceBox.SelectedIndex = 0; // loopback — hears whatever is playing

            foreach (BandSelect band in Enum.GetValues(typeof(BandSelect)))
                BandCombo.Items.Add(band);
            _loadingBands = false;

            // Coverage tabs follow the connection.
            _vm.PropertyChanged += OnVmPropertyChanged;

            PreviewKeyDown += OnPreviewKeyDown;
            Loaded += OnLoaded;
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(RadioViewModel.IsConnected)) return;
            if (_vm.IsConnected)
            {
                var cat = _vm.CurrentCat;
                Memory.Attach(cat, _vm.ReportError);
                Cw.Attach(cat, _vm.ReportError, _vm.PauseMonitorAsync, _vm.ResumeMonitor);
                Meters.Attach(cat, _vm.ReportError);
                Functions.Attach(cat, _vm.ReportError);
                Tests.Attach(cat, _vm.ReportError);
                Console.Attach(cat, _vm.ReportError);
            }
            else
            {
                Memory.Detach();
                Cw.Detach();
                Meters.Detach();
                Functions.Detach();
                Tests.Detach();
                Console.Detach();
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // `--sim` jumps straight into the built-in simulator (testing/demos).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--sim") >= 0)
                await _vm.ConnectAsync(new Models.ConnectionSettings { Kind = Models.ConnectionKind.BuiltInSimulator });
        }

        /// <summary>Last-resort error sink used by <see cref="App"/>.</summary>
        public void ReportUnhandledError(Exception ex) => _vm.ReportError(ex.Message);

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_vm.IsSweeping) { _vm.CancelSweepCommand.Execute(null); e.Handled = true; }
                else if (Disco.Visibility == Visibility.Visible)
                {
                    DiscoToggle.IsChecked = false; // Unchecked handler hides the overlay
                    e.Handled = true;
                }
            }
        }

        private async void OnConnectClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ConnectDialog(_vm.LoadLastConnection()) { Owner = this };
            if (dialog.ShowDialog() == true)
                await _vm.ConnectAsync(dialog.Result);
        }

        private async void OnBandPick(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingBands || BandCombo.SelectedItem == null) return;
            await _vm.SelectBandAsync((BandSelect)BandCombo.SelectedItem);
        }

        private void OnAudioToggle(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AudioToggle.IsChecked == true)
                {
                    if (_audio.IsRunning) return; // programmatic re-check
                    _audio.Start(AudioDeviceBox.SelectedIndex < 0 ? 0 : AudioDeviceBox.SelectedIndex);
                    AudioStatus.Text = $"capturing \"{AudioDeviceBox.SelectedItem}\"";
                }
                else
                {
                    _audio.Stop();
                    WaveView.ShowIdle();
                    AudioStatus.Text = "stopped";
                }
            }
            catch (InvalidOperationException ex)
            {
                _vm.ReportError(ex.Message);
                AudioStatus.Text = ex.Message;
                AudioToggle.IsChecked = false;
            }
        }

        private void OnChatterPick(object sender, SelectionChangedEventArgs e)
        {
            if (ChatterList.SelectedItem is ChatterHit hit)
            {
                ChatterList.SelectedItem = null;
                _vm.TuneTo(hit.Hz);
            }
        }

        private void OnDiscoToggle(object sender, RoutedEventArgs e)
            => Disco.Visibility = DiscoToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>A disco track started — make sure loopback capture is feeding the lights.</summary>
        private void EnsureLoopbackCapture()
        {
            if (_audio.IsRunning) return;
            AudioDeviceBox.SelectedIndex = 0;  // System audio (what you hear)
            AudioToggle.IsChecked = true;      // Checked handler starts the capture
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (_shutdownComplete) return;

            // Stop audio + monitor and release the port/simulator before really closing.
            e.Cancel = true;
            _audio.Dispose();
            await _vm.DisconnectAsync();
            _shutdownComplete = true;
            Close();
        }
    }
}
