using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FT891.Core;

namespace YS891.RadioUI.Views
{
    /// <summary>
    /// The function-test heatmap: one tile per library read command. Neon blue =
    /// pending, yellow = executing, green = pass, red = fail; a deliberate 100 ms
    /// cadence makes the walk across the API watchable. Click a tile for its result.
    /// </summary>
    public partial class TestsPanel : UserControl
    {
        private static readonly Brush PendingBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xBF, 0xFF));
        private static readonly Brush RunningBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD0, 0x00));
        private static readonly Brush PassBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x5A));
        private static readonly Brush FailBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A));

        private sealed class Tile : INotifyPropertyChanged
        {
            private Brush _brush = PendingBrush;
            private string _result = "pending";

            public Tile(string name, Func<ICatInterface, Task<string>> run)
            {
                Name = name;
                Run = run;
            }

            public string Name { get; }
            public Func<ICatInterface, Task<string>> Run { get; }
            public Brush Brush { get => _brush; set { _brush = value; Notify(nameof(Brush)); } }
            public string Result { get => _result; set { _result = value; Notify(nameof(Result), nameof(Tooltip)); } }
            public string Tooltip => $"{Name}: {Result}";

            public event PropertyChangedEventHandler PropertyChanged;
            private void Notify(params string[] names)
            {
                foreach (var n in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        private readonly ObservableCollection<Tile> _tiles = new ObservableCollection<Tile>();
        private ICatInterface _cat;
        private Action<string> _report = m => { };
        private bool _running;

        public TestsPanel()
        {
            InitializeComponent();
            foreach (var tile in BuildTiles()) _tiles.Add(tile);
            TileHost.ItemsSource = _tiles;
            IsEnabled = false;
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
            IsEnabled = false;
        }

        // ----- the test table: every read command in the library --------------------

        private static List<Tile> BuildTiles()
        {
            string Hz(long v) => FrequencyFormat.ToFormattedString(v);

            return new List<Tile>
            {
                new Tile("RadioId", async c => await c.GetRadioIdAsync(CancellationToken.None)),
                new Tile("RadioInfo", async c => Fmt(await c.GetRadioInfoAsync(CancellationToken.None))),
                new Tile("OppositeVfo", async c => Fmt(await c.GetOppositeVfoInfoAsync(CancellationToken.None))),
                new Tile("VfoA Freq", async c => Hz(await c.GetVfoAFrequencyAsync(CancellationToken.None))),
                new Tile("VfoB Freq", async c => Hz(await c.GetVfoBFrequencyAsync(CancellationToken.None))),
                new Tile("Mode", async c => (await c.GetModeAsync(CancellationToken.None)).ToString()),
                new Tile("Memory Ch", async c => $"ch {await c.GetMemoryChannelAsync(CancellationToken.None)}"),
                new Tile("ReadMemory 1", async c => Fmt(await c.ReadMemoryAsync(1, CancellationToken.None))),
                new Tile("Split", async c => OnOff(await c.GetSplitAsync(CancellationToken.None))),
                new Tile("Transmitting", async c => OnOff(await c.IsTransmittingAsync(CancellationToken.None))),
                new Tile("Busy", async c => OnOff(await c.GetBusyAsync(CancellationToken.None))),
                new Tile("Power", async c => OnOff(await c.GetPowerAsync(CancellationToken.None))),
                new Tile("TX Power", async c => $"{await c.GetTxPowerAsync(CancellationToken.None)} W"),
                new Tile("S-Meter", async c => MeterScale.FormatSMeter(await c.GetSMeterAsync(CancellationToken.None))),
                new Tile("Meter SWR", async c => $"{(await c.ReadMeterAsync(MeterType.SWR, CancellationToken.None)).Value}"),
                new Tile("Meter ALC", async c => $"{(await c.ReadMeterAsync(MeterType.ALC, CancellationToken.None)).Value}"),
                new Tile("Meter Power", async c => $"{(await c.ReadMeterAsync(MeterType.Power, CancellationToken.None)).Value}"),
                new Tile("Meter Comp", async c => $"{(await c.ReadMeterAsync(MeterType.Comp, CancellationToken.None)).Value}"),
                new Tile("Meter ID", async c => $"{(await c.ReadMeterAsync(MeterType.ID, CancellationToken.None)).Value}"),
                new Tile("AF Gain", async c => $"{await c.GetAfGainAsync(CancellationToken.None)}"),
                new Tile("RF Gain", async c => $"{await c.GetRfGainAsync(CancellationToken.None)}"),
                new Tile("Mic Gain", async c => $"{await c.GetMicGainAsync(CancellationToken.None)}"),
                new Tile("Squelch", async c => $"{await c.GetSqlLevelAsync(CancellationToken.None)}"),
                new Tile("Monitor Lvl", async c => $"{await c.GetMonitorLevelAsync(CancellationToken.None)}"),
                new Tile("Attenuator", async c => $"{await c.GetRfAttenuatorAsync(CancellationToken.None)} dB"),
                new Tile("Preamp", async c => OnOff(await c.GetPreampAsync(CancellationToken.None))),
                new Tile("Tuner", async c => (await c.GetTunerStateAsync(CancellationToken.None)).ToString()),
                new Tile("AGC", async c => (await c.GetAgcModeAsync(CancellationToken.None)).ToString()),
                new Tile("NR", async c => OnOff(await c.GetNoiseReductionAsync(CancellationToken.None))),
                new Tile("NR Level", async c => $"{await c.GetNoiseReductionLevelAsync(CancellationToken.None)}"),
                new Tile("NB", async c => OnOff(await c.GetNoiseBlankerAsync(CancellationToken.None))),
                new Tile("NB Level", async c => $"{await c.GetNoiseBlankerLevelAsync(CancellationToken.None)}"),
                new Tile("Auto Notch", async c => OnOff(await c.GetAutoNotchAsync(CancellationToken.None))),
                new Tile("Man. Notch", async c => { var (on, f) = await c.GetManualNotchAsync(CancellationToken.None); return $"{OnOff(on)} {f * 10} Hz"; }),
                new Tile("Contour", async c => { var (on, f, w) = await c.GetContourAsync(CancellationToken.None); return $"{OnOff(on)} {f} Hz w{w}"; }),
                new Tile("Bandwidth", async c => $"index {await c.GetBandwidthAsync(CancellationToken.None)}"),
                new Tile("IF Shift", async c => { var (on, hz) = await c.GetIfShiftAsync(CancellationToken.None); return $"{OnOff(on)} {hz} Hz"; }),
                new Tile("Clarifier", async c => { var (on, hz) = await c.GetClarifierAsync(CancellationToken.None); return $"{OnOff(on)} {hz} Hz"; }),
                new Tile("Key Speed", async c => $"{await c.GetKeySpeedAsync(CancellationToken.None)} wpm"),
                new Tile("Key Pitch", async c => $"{await c.GetKeyPitchAsync(CancellationToken.None)} Hz"),
                new Tile("Break-in", async c => (await c.GetBreakInModeAsync(CancellationToken.None)).ToString()),
                new Tile("Semi Delay", async c => $"{await c.GetSemiBreakInDelayAsync(CancellationToken.None)} ms"),
                new Tile("CW Spot", async c => OnOff(await c.GetCwSpotAsync(CancellationToken.None))),
                new Tile("Keyer", async c => OnOff(await c.GetKeyerAsync(CancellationToken.None))),
                new Tile("Keyer Mem 1", async c => Quote(await c.GetKeyerMemoryAsync(1, CancellationToken.None))),
                new Tile("VOX", async c => OnOff(await c.GetVoxAsync(CancellationToken.None))),
                new Tile("VOX Gain", async c => $"{await c.GetVoxGainAsync(CancellationToken.None)}"),
                new Tile("VOX Delay", async c => $"{await c.GetVoxDelayAsync(CancellationToken.None)} ms"),
                new Tile("Speech Proc", async c => OnOff(await c.GetSpeechProcessorAsync(CancellationToken.None))),
                new Tile("Proc Levels", async c => { var (pin, pout) = await c.GetSpeechProcessorLevelAsync(CancellationToken.None); return $"in {pin} / out {pout}"; }),
                new Tile("CTCSS", async c => OnOff(await c.GetCtcssAsync(CancellationToken.None))),
                new Tile("CTCSS #", async c => $"#{await c.GetCtcssDcsNumberAsync(CancellationToken.None)}"),
                new Tile("Rpt Offset", async c => $"{await c.GetOffsetAsync(CancellationToken.None)} Hz"),
                new Tile("Scan Mode", async c => (await c.GetScanModeAsync(CancellationToken.None)).ToString()),
                new Tile("Dimmer", async c => $"{await c.GetDimmerAsync(CancellationToken.None)}"),
                new Tile("Lock", async c => OnOff(await c.GetLockAsync(CancellationToken.None))),
                new Tile("Fast Step", async c => OnOff(await c.GetFastStepAsync(CancellationToken.None))),
                new Tile("Auto Info", async c => OnOff(await c.GetAutoInformationAsync(CancellationToken.None))),
                new Tile("Raw ID;", async c => Quote(await c.SendRawCommandAsync("ID;", -1, CancellationToken.None))),
                new Tile("Lib Diagnostic", async c =>
                {
                    var rows = await c.RunDiagnosticAsync(CancellationToken.None);
                    return $"{rows.Count} functions reported";
                }),
            };
        }

        private static string Fmt(RadioInfo info)
            => $"{FrequencyFormat.ToFormattedString(info.FrequencyHz)} {info.Mode} ch{info.MemoryChannel}{(info.IsTransmitting ? " TX" : "")}{(info.SplitActive ? " SPLIT" : "")}";

        private static string OnOff(bool b) => b ? "on" : "off";
        private static string Quote(string s) => string.IsNullOrEmpty(s) ? "(empty)" : $"\"{s}\"";

        // ----- run ----------------------------------------------------------------

        private async void OnRun(object sender, RoutedEventArgs e)
        {
            var cat = _cat;
            if (cat == null || _running) return;
            _running = true;
            RunButton.IsEnabled = false;

            foreach (var tile in _tiles)
            {
                tile.Brush = PendingBrush;
                tile.Result = "pending";
            }

            int pass = 0, fail = 0;
            foreach (var tile in _tiles)
            {
                if (_cat == null) break; // disconnected mid-run
                tile.Brush = RunningBrush;
                Detail.Text = $"Executing {tile.Name}…";
                try
                {
                    tile.Result = await tile.Run(cat);
                    tile.Brush = PassBrush;
                    pass++;
                }
                catch (FT891Exception ex)
                {
                    tile.Result = ex.Message;
                    tile.Brush = FailBrush;
                    fail++;
                }
                await Task.Delay(100); // deliberate cadence — let the heatmap breathe
            }

            Detail.Text = $"Done: {pass} passed, {fail} failed of {_tiles.Count}. Click a tile for its result.";
            _report($"Function tests: {pass} passed, {fail} failed.");
            RunButton.IsEnabled = true;
            _running = false;
        }

        private async void OnInitialize(object sender, RoutedEventArgs e)
        {
            var cat = _cat;
            if (cat == null) return;
            InitButton.IsEnabled = false;
            Detail.Text = "Calibrating against this radio (10 timed round-trips)…";
            try
            {
                int delay = await cat.InitializeLibraryAsync(10, CancellationToken.None);
                Detail.Text = $"Calibrated: inter-command delay settled at {delay} ms.";
            }
            catch (FT891Exception ex)
            {
                Detail.Text = $"Initialize failed: {ex.Message}";
            }
            finally
            {
                InitButton.IsEnabled = true;
            }
        }

        private void OnTileClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is Tile tile)
                Detail.Text = $"{tile.Name} → {tile.Result}";
        }
    }
}
