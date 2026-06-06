using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FT891.Core;
using YS891.RadioUI.Services;

namespace YS891.RadioUI.Views
{
    /// <summary>
    /// The deeper menu the real radio buries behind its F key — receiver, DSP,
    /// transmitter and system trims, every setter wired through a latest-value-
    /// wins coalescer. Attach/Detach follows the connection.
    /// </summary>
    public partial class FunctionsPanel : UserControl
    {
        private ICatInterface _cat;
        private Action<string> _report = m => { };
        private bool _loading = true;

        public FunctionsPanel()
        {
            InitializeComponent();
            IsEnabled = false;

            TunerCombo.ItemsSource = Enum.GetValues(typeof(TunerState));
            ScanCombo.ItemsSource = Enum.GetValues(typeof(ScanMode));

            WireRange(NrLevelSlider, FT891Ranges.NoiseReductionLevel, v => _cat.SetNoiseReductionLevelAsync(v, CancellationToken.None), v => NrLevelLabel.Text = $"NR level: {v}");
            WireRange(NbLevelSlider, FT891Ranges.NoiseBlankerLevel, v => _cat.SetNoiseBlankerLevelAsync(v, CancellationToken.None), v => NbLevelLabel.Text = $"NB level: {v}");
            WireRange(MonitorSlider, FT891Ranges.MonitorLevel, v => _cat.SetMonitorLevelAsync(v, CancellationToken.None), v => MonitorLabel.Text = $"Monitor level: {v}");
            WireRange(PowerSlider, FT891Ranges.TxPowerWatts, v => _cat.SetTxPowerAsync(v, CancellationToken.None), v => PowerLabel.Text = $"TX power: {v} W");
            WireRange(VoxGainSlider, FT891Ranges.VoxGain, v => _cat.SetVoxGainAsync(v, CancellationToken.None), v => VoxGainLabel.Text = $"VOX gain: {v}");
            WireRange(VoxDelaySlider, FT891Ranges.VoxDelayMs, v => _cat.SetVoxDelayAsync(v, CancellationToken.None), v => VoxDelayLabel.Text = $"VOX delay: {v} ms");
            WireRange(DimmerSlider, FT891Ranges.DimmerLevel, v => _cat.SetDimmerAsync(v, CancellationToken.None), v => DimmerLabel.Text = $"Dimmer: {v}");
            WireRange(CtcssNumSlider, FT891Ranges.CtcssDcsNumber, v => _cat.SetCtcssDcsNumberAsync(v, CancellationToken.None), v => CtcssNumLabel.Text = $"Tone #{v}");

            Wire(IfShiftSlider, v => _cat.SetIfShiftAsync(IfShiftCheck.IsChecked == true, v, CancellationToken.None), v => IfShiftLabel.Text = $"Shift: {v} Hz");
            Wire(ClarSlider, v => _cat.SetClarifierAsync(ClarCheck.IsChecked == true, v, CancellationToken.None), v => ClarLabel.Text = $"Offset: {v} Hz");
            Wire(ProcInSlider, v => _cat.SetSpeechProcessorLevelAsync(v, (int)ProcOutSlider.Value, CancellationToken.None), v => ProcInLabel.Text = $"Proc input: {v}");
            Wire(ProcOutSlider, v => _cat.SetSpeechProcessorLevelAsync((int)ProcInSlider.Value, v, CancellationToken.None), v => ProcOutLabel.Text = $"Proc output: {v}");
            Wire(NotchFreqSlider, v => _cat.SetManualNotchAsync(ManualNotchCheck.IsChecked == true, v, CancellationToken.None), v => NotchFreqLabel.Text = $"Notch: {v * 10} Hz");
            Wire(ContourFreqSlider, v => _cat.SetContourAsync(ContourCheck.IsChecked == true, v, (int)ContourWidthSlider.Value, CancellationToken.None), v => ContourFreqLabel.Text = $"Contour freq: {v} Hz");
            Wire(ContourWidthSlider, v => _cat.SetContourAsync(ContourCheck.IsChecked == true, (int)ContourFreqSlider.Value, v, CancellationToken.None), v => ContourWidthLabel.Text = $"Contour width: {v}");
            Wire(BandwidthSlider, v => _cat.SetBandwidthAsync(v, CancellationToken.None), v => BandwidthLabel.Text = $"IF bandwidth: index {v}");
            Wire(OffsetSlider, v => _cat.SetOffsetAsync(v * 1000, CancellationToken.None), v => OffsetLabel.Text = $"{v} kHz");
        }

        internal void Attach(ICatInterface cat, Action<string> report)
        {
            _cat = cat;
            _report = report ?? (m => { });
            IsEnabled = true;
            _ = LoadAsync();
        }

        internal void Detach()
        {
            _cat = null;
            _loading = true;
            IsEnabled = false;
        }

        // ----- wiring helpers ----------------------------------------------------

        private void WireRange(Slider slider, IntRange range, Func<int, Task> setter, Action<int> label)
        {
            slider.Minimum = range.Min;
            slider.Maximum = range.Max;
            slider.TickFrequency = 1;
            Wire(slider, setter, label);
        }

        private void Wire(Slider slider, Func<int, Task> setter, Action<int> label)
        {
            var coalescer = new CommandCoalescer(v => _cat == null ? Task.CompletedTask : setter((int)v), m => _report(m));
            slider.ValueChanged += (s, e) =>
            {
                label((int)e.NewValue);
                if (!_loading) coalescer.Request((long)e.NewValue);
            };
        }

        private async Task Apply(Func<Task> action)
        {
            if (_loading || _cat == null) return;
            try { await action(); }
            catch (FT891Exception ex) { _report(ex.Message); }
        }

        // ----- initial read ---------------------------------------------------------

        private async Task LoadAsync()
        {
            _loading = true;
            _report("Reading current settings…");

            await TryLoad(async () => PreampCheck.IsChecked = await _cat.GetPreampAsync(CancellationToken.None));
            await TryLoad(async () => AttCheck.IsChecked = await _cat.GetRfAttenuatorAsync(CancellationToken.None) > 0);
            await TryLoad(async () => NrLevelSlider.Value = await _cat.GetNoiseReductionLevelAsync(CancellationToken.None));
            await TryLoad(async () => NbLevelSlider.Value = await _cat.GetNoiseBlankerLevelAsync(CancellationToken.None));
            await TryLoad(async () => MonitorSlider.Value = await _cat.GetMonitorLevelAsync(CancellationToken.None));
            await TryLoad(async () => { var (on, hz) = await _cat.GetIfShiftAsync(CancellationToken.None); IfShiftCheck.IsChecked = on; IfShiftSlider.Value = hz; });
            await TryLoad(async () => { var (on, hz) = await _cat.GetClarifierAsync(CancellationToken.None); ClarCheck.IsChecked = on; ClarSlider.Value = hz; });
            await TryLoad(async () => AutoNotchCheck.IsChecked = await _cat.GetAutoNotchAsync(CancellationToken.None));
            await TryLoad(async () => { var (on, f) = await _cat.GetManualNotchAsync(CancellationToken.None); ManualNotchCheck.IsChecked = on; NotchFreqSlider.Value = Math.Max(1, f); });
            await TryLoad(async () => { var (on, f, w) = await _cat.GetContourAsync(CancellationToken.None); ContourCheck.IsChecked = on; ContourFreqSlider.Value = Math.Max(10, f); ContourWidthSlider.Value = Math.Max(1, w); });
            await TryLoad(async () => BandwidthSlider.Value = await _cat.GetBandwidthAsync(CancellationToken.None));
            await TryLoad(async () => PowerSlider.Value = await _cat.GetTxPowerAsync(CancellationToken.None));
            await TryLoad(async () => ProcCheck.IsChecked = await _cat.GetSpeechProcessorAsync(CancellationToken.None));
            await TryLoad(async () => { var (pin, pout) = await _cat.GetSpeechProcessorLevelAsync(CancellationToken.None); ProcInSlider.Value = pin; ProcOutSlider.Value = pout; });
            await TryLoad(async () => VoxCheck.IsChecked = await _cat.GetVoxAsync(CancellationToken.None));
            await TryLoad(async () => VoxGainSlider.Value = await _cat.GetVoxGainAsync(CancellationToken.None));
            await TryLoad(async () => VoxDelaySlider.Value = await _cat.GetVoxDelayAsync(CancellationToken.None));
            await TryLoad(async () => TunerCombo.SelectedItem = await _cat.GetTunerStateAsync(CancellationToken.None));
            await TryLoad(async () => OffsetSlider.Value = await _cat.GetOffsetAsync(CancellationToken.None) / 1000);
            await TryLoad(async () => CtcssCheck.IsChecked = await _cat.GetCtcssAsync(CancellationToken.None));
            await TryLoad(async () => CtcssNumSlider.Value = await _cat.GetCtcssDcsNumberAsync(CancellationToken.None));
            await TryLoad(async () => DimmerSlider.Value = await _cat.GetDimmerAsync(CancellationToken.None));
            await TryLoad(async () => FastStepCheck.IsChecked = await _cat.GetFastStepAsync(CancellationToken.None));
            await TryLoad(async () => AutoInfoCheck.IsChecked = await _cat.GetAutoInformationAsync(CancellationToken.None));
            await TryLoad(async () => ScanCombo.SelectedItem = await _cat.GetScanModeAsync(CancellationToken.None));

            _loading = false;
            _report("Functions ready — changes apply immediately.");
        }

        private async Task TryLoad(Func<Task> read)
        {
            if (_cat == null) return;
            try { await read(); }
            catch (FT891Exception) { /* radio/simulator doesn't support this one — leave default */ }
        }

        // ----- toggle/combo/button handlers -------------------------------------------

        private async void OnPreamp(object s, RoutedEventArgs e) => await Apply(() => _cat.SetPreampAsync(PreampCheck.IsChecked == true, CancellationToken.None));
        private async void OnAtt(object s, RoutedEventArgs e) => await Apply(() => _cat.SetRfAttenuatorAsync(AttCheck.IsChecked == true ? 12 : 0, CancellationToken.None));
        private async void OnIfShift(object s, RoutedEventArgs e) => await Apply(() => _cat.SetIfShiftAsync(IfShiftCheck.IsChecked == true, (int)IfShiftSlider.Value, CancellationToken.None));
        private async void OnClarifier(object s, RoutedEventArgs e) => await Apply(() => _cat.SetClarifierAsync(ClarCheck.IsChecked == true, (int)ClarSlider.Value, CancellationToken.None));
        private async void OnClarUp(object s, RoutedEventArgs e) => await Apply(() => _cat.ClarifierUpAsync(10, CancellationToken.None));
        private async void OnClarDown(object s, RoutedEventArgs e) => await Apply(() => _cat.ClarifierDownAsync(10, CancellationToken.None));
        private async void OnClarClear(object s, RoutedEventArgs e) => await Apply(() => _cat.ClarifierClearAsync(CancellationToken.None));
        private async void OnAutoNotch(object s, RoutedEventArgs e) => await Apply(() => _cat.SetAutoNotchAsync(AutoNotchCheck.IsChecked == true, CancellationToken.None));
        private async void OnManualNotch(object s, RoutedEventArgs e) => await Apply(() => _cat.SetManualNotchAsync(ManualNotchCheck.IsChecked == true, (int)NotchFreqSlider.Value, CancellationToken.None));
        private async void OnContour(object s, RoutedEventArgs e) => await Apply(() => _cat.SetContourAsync(ContourCheck.IsChecked == true, (int)ContourFreqSlider.Value, (int)ContourWidthSlider.Value, CancellationToken.None));
        private async void OnProc(object s, RoutedEventArgs e) => await Apply(() => _cat.SetSpeechProcessorAsync(ProcCheck.IsChecked == true, CancellationToken.None));
        private async void OnVox(object s, RoutedEventArgs e) => await Apply(() => _cat.SetVoxAsync(VoxCheck.IsChecked == true, CancellationToken.None));
        private async void OnCtcss(object s, RoutedEventArgs e) => await Apply(() => _cat.SetCtcssAsync(CtcssCheck.IsChecked == true, CancellationToken.None));
        private async void OnFastStep(object s, RoutedEventArgs e) => await Apply(() => _cat.SetFastStepAsync(FastStepCheck.IsChecked == true, CancellationToken.None));
        private async void OnAutoInfo(object s, RoutedEventArgs e) => await Apply(() => _cat.SetAutoInformationAsync(AutoInfoCheck.IsChecked == true, CancellationToken.None));

        private async void OnTuner(object s, SelectionChangedEventArgs e)
        {
            if (_loading || TunerCombo.SelectedItem == null) return;
            await Apply(() => _cat.SetTunerStateAsync((TunerState)TunerCombo.SelectedItem, CancellationToken.None));
        }

        private async void OnScan(object s, SelectionChangedEventArgs e)
        {
            if (_loading || ScanCombo.SelectedItem == null) return;
            await Apply(() => _cat.SetScanModeAsync((ScanMode)ScanCombo.SelectedItem, CancellationToken.None));
        }

        private async void OnPowerOff(object s, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                "Send PS0 and switch the radio off?\n\nYou'll need the radio's front power button to switch it back on.",
                "Power off radio", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Yes)
                await Apply(() => _cat.SetPowerAsync(false, CancellationToken.None));
        }
    }
}
