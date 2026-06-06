using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FT891.Core;
using FT891.Simulator.Morse;

namespace YS891.RadioUI.Views
{
    /// <summary>
    /// CW sending, keyer memories, zero-in, voice message memory — and a live
    /// Morse decoder ported from the Demo: it fast-polls the S-meter and treats
    /// its rise/fall as keying, so it works over pure CAT on real hardware too.
    /// </summary>
    public partial class CwPanel : UserControl
    {
        private const long BeaconHz = 14_058_000; // where the built-in simulator keys its beacon

        private ICatInterface _cat;
        private Action<string> _report = m => { };
        private Func<Task> _pauseMonitor = () => Task.CompletedTask;
        private Action _resumeMonitor = () => { };
        private CancellationTokenSource _decodeCts;
        private readonly TextBox[] _keyerBoxes;

        public CwPanel()
        {
            InitializeComponent();
            IsEnabled = false;

            int slots = FT891Ranges.KeyerMemorySlot.Max;
            _keyerBoxes = new TextBox[slots];
            for (int slot = 1; slot <= slots; slot++)
            {
                int captured = slot;
                var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                var label = new TextBlock
                {
                    Text = $"{slot}:",
                    Width = 22,
                    Foreground = (System.Windows.Media.Brush)FindResource("ButtonTextBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                DockPanel.SetDock(label, Dock.Left);
                var read = new Button { Content = "READ", Style = (Style)FindResource("PanelButton"), Margin = new Thickness(4, 0, 0, 0) };
                DockPanel.SetDock(read, Dock.Right);
                var write = new Button { Content = "WRITE", Style = (Style)FindResource("PanelButton"), Margin = new Thickness(4, 0, 0, 0) };
                DockPanel.SetDock(write, Dock.Right);
                var box = new TextBox { FontFamily = new System.Windows.Media.FontFamily("Consolas"), MaxLength = 50 };
                _keyerBoxes[slot - 1] = box;

                read.Click += async (s, e) => await Apply($"Read keyer memory {captured}", async c =>
                    box.Text = await c.GetKeyerMemoryAsync(captured, CancellationToken.None));
                write.Click += async (s, e) => await Apply($"Write keyer memory {captured}", c =>
                    c.SetKeyerMemoryAsync(captured, box.Text ?? "", CancellationToken.None));

                row.Children.Add(label);
                row.Children.Add(read);
                row.Children.Add(write);
                row.Children.Add(box);
                KeyerRows.Children.Add(row);
            }

            for (int i = FT891Ranges.VoiceMemorySlot.Min; i <= FT891Ranges.VoiceMemorySlot.Max; i++)
                VoiceSlot.Items.Add(i);
            VoiceSlot.SelectedIndex = 0;
        }

        internal void Attach(ICatInterface cat, Action<string> report, Func<Task> pauseMonitor = null, Action resumeMonitor = null)
        {
            _cat = cat;
            _report = report ?? (m => { });
            _pauseMonitor = pauseMonitor ?? (() => Task.CompletedTask);
            _resumeMonitor = resumeMonitor ?? (() => { });
            IsEnabled = true;
        }

        internal void Detach()
        {
            _decodeCts?.Cancel();
            _cat = null;
            IsEnabled = false;
        }

        private async Task Apply(string what, Func<ICatInterface, Task> action)
        {
            var cat = _cat;
            if (cat == null) return;
            try
            {
                await action(cat);
                _report($"{what}: done.");
            }
            catch (FT891Exception ex)
            {
                _report($"{what}: {ex.Message}");
            }
        }

        private async void OnSendCw(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CwText.Text)) { _report("Nothing to send."); return; }
            await Apply($"Send CW \"{CwText.Text}\"", c => c.SendCwAsync(CwText.Text, CancellationToken.None));
        }

        private async void OnZeroIn(object s, RoutedEventArgs e)
            => await Apply("Zero-in", c => c.ZeroInAsync(CancellationToken.None));

        private async void OnLoadMessage(object s, RoutedEventArgs e)
        {
            int slot = (int)VoiceSlot.SelectedItem;
            VoiceStatus.Text = $"Recording into slot {slot} — speak into the mic.";
            await Apply($"Load voice message {slot}", c => c.LoadMessageAsync(slot, CancellationToken.None));
        }

        private async void OnPlayback(object s, RoutedEventArgs e)
        {
            int slot = (int)VoiceSlot.SelectedItem;
            VoiceStatus.Text = $"Playing back slot {slot} — this transmits!";
            await Apply($"Playback voice message {slot}", c => c.PlaybackAsync(slot, CancellationToken.None));
        }

        // ----- morse decoder (ported from FT891.Demo MorseDecoderScreen) ------------

        private async void OnTuneBeacon(object s, RoutedEventArgs e)
            => await Apply($"Tune to beacon {FrequencyFormat.ToFormattedString(BeaconHz)}", async c =>
            {
                await c.SetVfoAFrequencyAsync(BeaconHz, CancellationToken.None);
                await c.SetModeAsync(OperatingMode.CW, CancellationToken.None);
            });

        private bool _decodeRunning;

        private async void OnDecodeToggle(object s, RoutedEventArgs e)
        {
            if (DecodeToggle.IsChecked == true)
            {
                if (_decodeRunning) return; // programmatic re-check
                _decodeRunning = true;
                try { await RunDecoderAsync(); }
                finally { _decodeRunning = false; }
            }
            else
            {
                _decodeCts?.Cancel();
            }
        }

        private async Task RunDecoderAsync()
        {
            var cat = _cat;
            if (cat == null) { DecodeToggle.IsChecked = false; return; }

            int wpm = int.TryParse(WpmBox.Text, out int parsed) ? Math.Max(4, Math.Min(60, parsed)) : 16;
            var decoder = new MorseDecoder(MorseCode.UnitMs(wpm));
            var onBrush = (Brush)FindResource("IndicatorOnBrush");
            var offBrush = (Brush)FindResource("IndicatorOffBrush");

            _decodeCts = new CancellationTokenSource();
            var ct = _decodeCts.Token;
            await _pauseMonitor(); // the decoder needs the wire to itself for clean timing
            _report($"Morse decoder running at {wpm} wpm — watching the S-meter for keying.");
            try
            {
                int peak = 0;
                var render = Stopwatch.StartNew();
                while (!ct.IsCancellationRequested && _cat != null)
                {
                    int sm = await cat.GetSMeterAsync(ct);
                    peak = Math.Max(peak, sm);
                    int threshold = Math.Max(40, peak / 2); // adaptive, like the Demo
                    bool keyDown = sm > threshold;
                    decoder.Sample(keyDown, Environment.TickCount);

                    if (render.ElapsedMilliseconds >= 150)
                    {
                        render.Restart();
                        KeyLamp.Fill = keyDown ? onBrush : offBrush;
                        DecodedText.Text = Tail(MorseAbbreviations.Expand(decoder.Text), 400);
                        RawCode.Text = Tail(decoder.Code, 400);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped on purpose.
            }
            catch (FT891Exception ex)
            {
                _report($"Decoder: {ex.Message}");
            }
            finally
            {
                decoder.Flush();
                DecodedText.Text = Tail(MorseAbbreviations.Expand(decoder.Text), 400);
                RawCode.Text = Tail(decoder.Code, 400);
                KeyLamp.Fill = offBrush;
                _decodeCts.Dispose();
                _decodeCts = null;
                DecodeToggle.IsChecked = false;
                _resumeMonitor();
                _report("Morse decoder stopped.");
            }
        }

        private static string Tail(string text, int max)
            => string.IsNullOrEmpty(text) ? "—" : text.Length <= max ? text : text.Substring(text.Length - max);
    }
}
