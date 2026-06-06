using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using FT891.Core;
using YS891.RadioUI.Models;
using YS891.RadioUI.Services;

namespace YS891.RadioUI.ViewModels
{
    /// <summary>Parameters the MULTI knob can drive.</summary>
    internal enum MultiParameter
    {
        AfGain,
        RfGain,
        MicGain,
        Squelch,
        TxPower,
    }

    /// <summary>MOX confirm-gate states: a stray click must never key the rig.</summary>
    internal enum MoxState
    {
        Off,
        Armed,
        Transmitting,
    }

    /// <summary>One active frequency found by the chatter scan.</summary>
    internal sealed class ChatterHit
    {
        public ChatterHit(long hz, int raw)
        {
            Hz = hz;
            Label = $"{FrequencyFormat.ToFormattedString(hz)}   {MeterScale.FormatSMeter(raw)}";
        }

        public long Hz { get; }
        public string Label { get; }
    }

    /// <summary>
    /// The hub between the panel and the radio: wraps ICatInterface + RadioMonitor,
    /// keeps optimistic local state, coalesces dial input, and guards against
    /// monitor echoes overwriting in-flight user intent.
    /// </summary>
    internal sealed class RadioViewModel : ObservableObject
    {
        // FT-891 receiver coverage.
        private const long MinHz = 30_000;
        private const long MaxHz = 56_000_000;

        private static readonly OperatingMode[] ModeCycle =
        {
            OperatingMode.LSB, OperatingMode.USB, OperatingMode.CW,
            OperatingMode.AM, OperatingMode.FM, OperatingMode.DATA_USB,
        };

        private static readonly AgcMode[] AgcCycle =
        {
            AgcMode.Auto, AgcMode.Fast, AgcMode.Mid, AgcMode.Slow,
        };

        private readonly SettingsStore _settings = new SettingsStore();
        private readonly CommandCoalescer _freqCoalescer;
        private readonly CommandCoalescer _multiCoalescer;
        private readonly EchoGuard<long> _freqEcho = new EchoGuard<long>();
        private readonly EchoGuard<OperatingMode> _modeEcho = new EchoGuard<OperatingMode>();
        private readonly EchoGuard<bool> _splitEcho = new EchoGuard<bool>();
        private readonly DispatcherTimer _moxDisarmTimer;

        private RadioConnection _connection;
        private RadioMonitor _monitor;
        private CancellationTokenSource _sweepCts;

        private bool _isConnected;
        private string _connectionDescription = "";
        private string _statusMessage = "Not connected — press CONNECT.";
        private long _frequencyHz = 14_250_000;
        private OperatingMode _mode = OperatingMode.USB;
        private AgcMode _agcMode = AgcMode.Auto;
        private int _memoryChannel = 1;
        private bool _isSplit;
        private bool _isTransmitting;
        private bool _isLocked;
        private bool _nbOn;
        private bool _nrOn;
        private int _sMeterRaw;
        private long _stepHz = 100;
        private MultiParameter _multiParameter = MultiParameter.AfGain;
        private int _multiValue;
        private bool _isSweeping;
        private MoxState _moxState = MoxState.Off;

        private static readonly long[] SpanOptionsHz = { 25_000, 100_000, 500_000, 2_000_000 };
        private int _spanIndex = 1;
        private bool _isChatterVisible;

        public RadioViewModel()
        {
            _freqCoalescer = new CommandCoalescer(WriteFrequencyAsync, ReportError);
            _multiCoalescer = new CommandCoalescer(WriteMultiValueAsync, ReportError);
            _moxDisarmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _moxDisarmTimer.Tick += (s, e) =>
            {
                _moxDisarmTimer.Stop();
                if (MoxStateValue == MoxState.Armed) MoxStateValue = MoxState.Off;
            };

            DisconnectCommand = new AsyncRelayCommand(DisconnectAsync, ReportError, () => IsConnected);
            BandUpCommand = Radio(c => c.BandUpAsync(CancellationToken.None));
            BandDownCommand = Radio(c => c.BandDownAsync(CancellationToken.None));
            SwapVfosCommand = Radio(c => c.SwapVfosAsync(CancellationToken.None));
            CopyVfoAToBCommand = Radio(c => c.CopyVfoAToVfoBAsync(CancellationToken.None));
            CopyVfoBToACommand = Radio(c => c.CopyVfoBToVfoAAsync(CancellationToken.None));
            FineUpCommand = Radio(c => c.FrequencyUpAsync(CancellationToken.None));
            FineDownCommand = Radio(c => c.FrequencyDownAsync(CancellationToken.None));
            ModeCommand = new AsyncRelayCommand(CycleModeAsync, ReportError, () => CanOperate);
            AgcCommand = new AsyncRelayCommand(CycleAgcAsync, ReportError, () => CanOperate);
            LockCommand = new AsyncRelayCommand(ToggleLockAsync, ReportError, () => CanOperate);
            SplitCommand = new AsyncRelayCommand(ToggleSplitAsync, ReportError, () => CanOperate);
            NbCommand = new AsyncRelayCommand(ToggleNbAsync, ReportError, () => CanOperate);
            NrCommand = new AsyncRelayCommand(ToggleNrAsync, ReportError, () => CanOperate);
            StepCommand = new AsyncRelayCommand(CycleStepAsync, ReportError, () => CanOperate);
            MultiSelectCommand = new AsyncRelayCommand(CycleMultiParameterAsync, ReportError, () => CanOperate);
            MoxCommand = new AsyncRelayCommand(HandleMoxAsync, ReportError, () => CanOperate);
            SweepCommand = new AsyncRelayCommand(RunSweepAsync, ReportError, () => CanOperate);
            CancelSweepCommand = new AsyncRelayCommand(CancelSweepAsync, ReportError, () => IsSweeping);
            SpanCommand = new AsyncRelayCommand(CycleSpanAsync, ReportError, () => !IsSweeping);
            ChatterCommand = new AsyncRelayCommand(RunChatterAsync, ReportError, () => CanOperate);
            CloseChatterCommand = new AsyncRelayCommand(CloseChatterAsync, ReportError);
            HeatmapCommand = new AsyncRelayCommand(RunHeatmapAsync, ReportError, () => CanOperate);
        }

        // ----- bindable state -------------------------------------------------

        public bool IsConnected { get => _isConnected; private set { if (Set(ref _isConnected, value)) RefreshCommands(); } }
        public string ConnectionDescription { get => _connectionDescription; private set => Set(ref _connectionDescription, value); }
        public string StatusMessage { get => _statusMessage; private set => Set(ref _statusMessage, value); }
        public long FrequencyHz { get => _frequencyHz; private set { if (Set(ref _frequencyHz, value)) Raise(nameof(FrequencyText)); } }
        public string FrequencyText => FrequencyFormat.ToFormattedString(_frequencyHz);
        public OperatingMode Mode { get => _mode; private set { if (Set(ref _mode, value)) Raise(nameof(ModeText)); } }
        public string ModeText => _mode.ToString().Replace('_', '-');
        public AgcMode Agc { get => _agcMode; private set { if (Set(ref _agcMode, value)) Raise(nameof(AgcText)); } }
        public string AgcText => $"AGC {_agcMode.ToString().ToUpperInvariant()}";
        public int MemoryChannel { get => _memoryChannel; private set => Set(ref _memoryChannel, value); }
        public bool IsSplit { get => _isSplit; private set => Set(ref _isSplit, value); }
        public bool IsTransmitting { get => _isTransmitting; private set => Set(ref _isTransmitting, value); }
        public bool IsLocked { get => _isLocked; private set => Set(ref _isLocked, value); }
        public bool NbOn { get => _nbOn; private set => Set(ref _nbOn, value); }
        public bool NrOn { get => _nrOn; private set => Set(ref _nrOn, value); }
        public int SMeterRaw { get => _sMeterRaw; private set { if (Set(ref _sMeterRaw, value)) Raise(nameof(SMeterText)); } }
        public string SMeterText => MeterScale.FormatSMeter(_sMeterRaw);
        public long StepHz { get => _stepHz; private set { if (Set(ref _stepHz, value)) Raise(nameof(StepText)); } }
        public string StepText => $"STEP {TuningStep.Label(_stepHz)}";
        public MultiParameter Multi { get => _multiParameter; private set { if (Set(ref _multiParameter, value)) { Raise(nameof(MultiText)); } } }
        public int MultiValue { get => _multiValue; private set { if (Set(ref _multiValue, value)) Raise(nameof(MultiText)); } }
        public string MultiText => FormatMulti(_multiParameter, _multiValue);
        public bool IsSweeping { get => _isSweeping; private set { if (Set(ref _isSweeping, value)) RefreshCommands(); } }
        public MoxState MoxStateValue { get => _moxState; private set { if (Set(ref _moxState, value)) { Raise(nameof(MoxText)); Raise(nameof(IsMoxArmed)); } } }
        public string MoxText => _moxState == MoxState.Armed ? "CONFIRM" : "MOX";
        public bool IsMoxArmed => _moxState == MoxState.Armed;
        public long SpanHz => SpanOptionsHz[_spanIndex];
        public string SpanText => $"SPAN ±{(SpanHz / 2 >= 1_000_000 ? $"{SpanHz / 2_000_000.0:0.#}M" : $"{SpanHz / 2_000.0:0.#}k")}";
        public ObservableCollection<ChatterHit> ChatterHits { get; } = new ObservableCollection<ChatterHit>();
        public bool IsChatterVisible { get => _isChatterVisible; private set => Set(ref _isChatterVisible, value); }

        /// <summary>The live CAT link, for windows that talk to the radio directly.</summary>
        public ICatInterface CurrentCat => _connection?.Cat;

        /// <summary>Pause background polling so a panel can own the wire (fast S-meter loops).</summary>
        public Task PauseMonitorAsync() => _monitor?.StopAsync() ?? Task.CompletedTask;

        /// <summary>Resume background polling after <see cref="PauseMonitorAsync"/>.</summary>
        public void ResumeMonitor()
        {
            if (IsConnected && _monitor != null && !_monitor.IsRunning) _monitor.Start();
        }

        private bool CanOperate => IsConnected && !IsSweeping;

        // ----- events for the scope strip ------------------------------------

        public event Action<int> SMeterSample;
        public event Action<long, long> SweepStarted;
        public event Action<SweepPoint> SweepPointReceived;
        public event Action SweepPassStarted;
        public event Action SweepEnded;

        // ----- commands -------------------------------------------------------

        public ICommand DisconnectCommand { get; }
        public ICommand BandUpCommand { get; }
        public ICommand BandDownCommand { get; }
        public ICommand SwapVfosCommand { get; }
        public ICommand CopyVfoAToBCommand { get; }
        public ICommand CopyVfoBToACommand { get; }
        public ICommand FineUpCommand { get; }
        public ICommand FineDownCommand { get; }
        public ICommand ModeCommand { get; }
        public ICommand AgcCommand { get; }
        public ICommand LockCommand { get; }
        public ICommand SplitCommand { get; }
        public ICommand NbCommand { get; }
        public ICommand NrCommand { get; }
        public ICommand StepCommand { get; }
        public ICommand MultiSelectCommand { get; }
        public ICommand MoxCommand { get; }
        public ICommand SweepCommand { get; }
        public ICommand CancelSweepCommand { get; }
        public ICommand SpanCommand { get; }
        public ICommand ChatterCommand { get; }
        public ICommand CloseChatterCommand { get; }
        public ICommand HeatmapCommand { get; }

        public ConnectionSettings LoadLastConnection() => _settings.LoadConnection();

        // ----- connect / disconnect ------------------------------------------

        public async Task ConnectAsync(ConnectionSettings settings)
        {
            if (IsConnected) await DisconnectAsync();

            StatusMessage = $"Connecting to {settings}…";
            RadioConnection connection = null;
            try
            {
                connection = RadioConnectionFactory.Create(settings);
                var cat = connection.Cat;
                await Task.Run(() => cat.Connect());

                // Prime state the monitor doesn't poll.
                Agc = await cat.GetAgcModeAsync(CancellationToken.None);
                IsLocked = await cat.GetLockAsync(CancellationToken.None);
                NbOn = await cat.GetNoiseBlankerAsync(CancellationToken.None);
                NrOn = await cat.GetNoiseReductionAsync(CancellationToken.None);
                MultiValue = await ReadMultiValueAsync(cat, Multi);

                // Monitor must be configured before Start, and started on the UI
                // thread so it captures the dispatcher's SynchronizationContext.
                var monitor = new RadioMonitor(cat)
                {
                    PollIntervalMs = 250,
                    IncludeSMeter = true,
                    UseSynchronizationContext = true,
                };
                monitor.FrequencyChanged += OnFrequencyChanged;
                monitor.ModeChanged += OnModeChanged;
                monitor.TransmitChanged += OnTransmitChanged;
                monitor.SplitChanged += OnSplitChanged;
                monitor.MemoryChannelChanged += ch => MemoryChannel = ch;
                monitor.SMeterChanged += OnSMeterChanged;
                monitor.MonitorError += e => ReportError($"Monitor: {e.Message}");
                monitor.Start();

                _connection = connection;
                _monitor = monitor;
                ConnectionDescription = connection.Description;
                IsConnected = true;
                StatusMessage = $"Connected — {connection.Description}.";
                _settings.SaveConnection(settings);
            }
            catch (FT891Exception ex)
            {
                connection?.Dispose();
                StatusMessage = $"Connect failed: {ex.Message}";
            }
        }

        public async Task DisconnectAsync()
        {
            _sweepCts?.Cancel();
            if (_monitor != null)
            {
                await _monitor.StopAsync();
                _monitor.Dispose();
                _monitor = null;
            }
            _connection?.Dispose();
            _connection = null;
            _freqCoalescer.Reset();
            _multiCoalescer.Reset();
            _freqEcho.Reset();
            _modeEcho.Reset();
            _splitEcho.Reset();
            MoxStateValue = MoxState.Off;
            IsTransmitting = false;
            IsConnected = false;
            ConnectionDescription = "";
            StatusMessage = "Disconnected.";
        }

        // ----- dial + MULTI input --------------------------------------------

        /// <summary>Main dial turned by <paramref name="detents"/> clicks (signed).</summary>
        public void OnDialDetents(int detents)
        {
            if (!CanOperate || IsLocked || detents == 0) return;
            long target = Math.Max(MinHz, Math.Min(MaxHz, FrequencyHz + detents * StepHz));
            FrequencyHz = target;          // optimistic — display follows the hand
            _freqCoalescer.Request(target);
        }

        /// <summary>MULTI knob turned by <paramref name="detents"/> clicks (signed).</summary>
        public void OnMultiDetents(int detents)
        {
            if (!CanOperate || detents == 0) return;
            var range = RangeFor(Multi);
            int target = range.Clamp(MultiValue + detents * StepFor(Multi));
            MultiValue = target;
            _multiCoalescer.Request(target);
        }

        private async Task WriteFrequencyAsync(long hz)
        {
            var cat = _connection?.Cat;
            if (cat == null) return;
            _freqEcho.BeginWrite(hz);
            try { await cat.SetVfoAFrequencyAsync(hz, CancellationToken.None); }
            finally { _freqEcho.EndWrite(); }
        }

        private async Task WriteMultiValueAsync(long value)
        {
            var cat = _connection?.Cat;
            if (cat == null) return;
            await SetMultiValueAsync(cat, Multi, (int)value);
        }

        // ----- button handlers -------------------------------------------------

        private async Task CycleModeAsync()
        {
            int index = Array.IndexOf(ModeCycle, Mode);
            var next = ModeCycle[(index + 1 + ModeCycle.Length) % ModeCycle.Length];
            Mode = next;
            _modeEcho.BeginWrite(next);
            try { await _connection.Cat.SetModeAsync(next, CancellationToken.None); }
            finally { _modeEcho.EndWrite(); }
        }

        private async Task CycleAgcAsync()
        {
            int index = Array.IndexOf(AgcCycle, Agc);
            var next = AgcCycle[(index + 1 + AgcCycle.Length) % AgcCycle.Length];
            Agc = next;
            await _connection.Cat.SetAgcModeAsync(next, CancellationToken.None);
        }

        private async Task ToggleLockAsync()
        {
            bool next = !IsLocked;
            IsLocked = next;
            await _connection.Cat.SetLockAsync(next, CancellationToken.None);
        }

        private async Task ToggleSplitAsync()
        {
            bool next = !IsSplit;
            IsSplit = next;
            _splitEcho.BeginWrite(next);
            try { await _connection.Cat.SetSplitAsync(next, CancellationToken.None); }
            finally { _splitEcho.EndWrite(); }
        }

        private async Task ToggleNbAsync()
        {
            bool next = !NbOn;
            NbOn = next;
            await _connection.Cat.SetNoiseBlankerAsync(next, CancellationToken.None);
        }

        private async Task ToggleNrAsync()
        {
            bool next = !NrOn;
            NrOn = next;
            await _connection.Cat.SetNoiseReductionAsync(next, CancellationToken.None);
        }

        private Task CycleStepAsync()
        {
            StepHz = TuningStep.Next(StepHz);
            return Task.CompletedTask;
        }

        private async Task CycleMultiParameterAsync()
        {
            var values = (MultiParameter[])Enum.GetValues(typeof(MultiParameter));
            Multi = values[((int)Multi + 1) % values.Length];
            MultiValue = await ReadMultiValueAsync(_connection.Cat, Multi);
        }

        private async Task HandleMoxAsync()
        {
            switch (MoxStateValue)
            {
                case MoxState.Off:
                    MoxStateValue = MoxState.Armed;
                    StatusMessage = "MOX armed — press again within 3 s to transmit.";
                    _moxDisarmTimer.Stop();
                    _moxDisarmTimer.Start();
                    break;

                case MoxState.Armed:
                    _moxDisarmTimer.Stop();
                    await _connection.Cat.SetMoxAsync(true, CancellationToken.None);
                    MoxStateValue = MoxState.Transmitting;
                    StatusMessage = "TRANSMITTING — press MOX to return to receive.";
                    break;

                case MoxState.Transmitting:
                    await _connection.Cat.SetMoxAsync(false, CancellationToken.None);
                    MoxStateValue = MoxState.Off;
                    StatusMessage = "Back to receive.";
                    break;
            }
        }

        // ----- sweep / chatter ---------------------------------------------------

        private Task CycleSpanAsync()
        {
            _spanIndex = (_spanIndex + 1) % SpanOptionsHz.Length;
            Raise(nameof(SpanText));
            Raise(nameof(SpanHz));
            return Task.CompletedTask;
        }

        private Task RunSweepAsync()
            => RunScanAsync("Sweep", null, includeBusy: false, passes: 1);

        private Task RunChatterAsync()
            => RunScanAsync("Chatter scan", AnalyzeChatter, includeBusy: true, passes: 1);

        /// <summary>
        /// Continuous heatmap: sweep pass after pass until cancelled. 100 points per
        /// pass — coarser grids step right over narrow signals between samples.
        /// </summary>
        private Task RunHeatmapAsync()
            => RunScanAsync("Heatmap", null, includeBusy: false, passes: int.MaxValue);

        /// <summary>
        /// Shared scan core: pause the monitor, sweep the configured span around
        /// the VFO feeding the scope (for one or many passes), then optionally
        /// hand the collected points to an analyzer (chatter peak-finding).
        /// </summary>
        private async Task RunScanAsync(string what, Action<List<SweepPoint>> analyze, bool includeBusy, int passes, int pointsPerPass = 100)
        {
            long span = SpanHz;
            long stepHz = Math.Max(500, span / pointsPerPass);

            long center = FrequencyHz;
            long low = Math.Max(MinHz, center - span / 2);
            long high = Math.Min(MaxHz, center + span / 2);

            IsSweeping = true;
            IsChatterVisible = false;
            StatusMessage = $"{what} {FrequencyFormat.ToMegahertzString(low, 3)}–{FrequencyFormat.ToMegahertzString(high, 3)}… receive is interrupted.";
            _sweepCts = new CancellationTokenSource();

            await _monitor.StopAsync();   // the scan owns the wire
            SweepStarted?.Invoke(low, high);
            var points = new List<SweepPoint>();
            try
            {
                var progress = new Progress<SweepPoint>(p =>
                {
                    points.Add(p);
                    SweepPointReceived?.Invoke(p);
                });
                for (int pass = 0; pass < passes; pass++)
                {
                    _sweepCts.Token.ThrowIfCancellationRequested();
                    points.Clear();
                    if (pass > 0) SweepPassStarted?.Invoke();
                    await SweepService.RunAsync(_connection.Cat, low, high, stepHz, progress, _sweepCts.Token, includeBusy);
                }
                StatusMessage = $"{what} complete.";
                analyze?.Invoke(points);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = $"{what} cancelled.";
            }
            finally
            {
                _sweepCts.Dispose();
                _sweepCts = null;
                SweepEnded?.Invoke();
                if (_monitor != null && IsConnected) _monitor.Start();
                IsSweeping = false;
            }
        }

        /// <summary>
        /// Find the chatter: peaks clearly above the noise floor (median + margin),
        /// strongest first, suppressing shoulders next to an already-taken peak.
        /// </summary>
        private void AnalyzeChatter(List<SweepPoint> points)
        {
            ChatterHits.Clear();
            if (points.Count < 5)
            {
                StatusMessage = "Chatter scan: not enough points.";
                return;
            }

            var sorted = points.Select(p => p.Raw).OrderBy(r => r).ToArray();
            int floor = sorted[sorted.Length / 2];
            int threshold = floor + Math.Max(25, floor / 2);

            var hits = new List<SweepPoint>();
            for (int i = 1; i < points.Count - 1; i++)
            {
                var p = points[i];
                // A hit is a local maximum that's either clearly above the noise
                // floor or that the radio itself flagged busy (squelch open).
                if (p.Raw < threshold && !p.Busy) continue;
                if (p.Raw < points[i - 1].Raw || p.Raw < points[i + 1].Raw) continue; // local max only
                hits.Add(p);
            }

            foreach (var hit in hits.OrderByDescending(h => h.Raw).Take(12))
                ChatterHits.Add(new ChatterHit(hit.Hz, hit.Raw));

            IsChatterVisible = ChatterHits.Count > 0;
            StatusMessage = ChatterHits.Count > 0
                ? $"Found chatter on {ChatterHits.Count} frequencies — click one to tune."
                : $"No chatter above the noise floor (S-floor {floor}, threshold {threshold}).";
        }

        /// <summary>Jump to a band via the radio's band-stacking registers.</summary>
        public async Task SelectBandAsync(BandSelect band)
        {
            if (!CanOperate) return;
            try
            {
                await _connection.Cat.SelectBandAsync(band, CancellationToken.None);
                StatusMessage = $"Band: {band}.";
            }
            catch (FT891Exception ex)
            {
                ReportError(ex.Message);
            }
        }

        /// <summary>Jump straight to a found frequency (chatter list click).</summary>
        public void TuneTo(long hz)
        {
            if (!IsConnected) return;
            long target = Math.Max(MinHz, Math.Min(MaxHz, hz));
            FrequencyHz = target;
            _freqCoalescer.Request(target);
            IsChatterVisible = false;
            StatusMessage = $"Tuned to {FrequencyFormat.ToFormattedString(target)}.";
        }

        private Task CloseChatterAsync()
        {
            IsChatterVisible = false;
            return Task.CompletedTask;
        }

        private Task CancelSweepAsync()
        {
            _sweepCts?.Cancel();
            return Task.CompletedTask;
        }

        // ----- monitor handlers ---------------------------------------------------

        private void OnFrequencyChanged(long hz)
        {
            if (_freqCoalescer.IsBusy || _freqEcho.ShouldIgnore(hz)) return;
            FrequencyHz = hz;
        }

        private void OnModeChanged(OperatingMode mode)
        {
            if (_modeEcho.ShouldIgnore(mode)) return;
            Mode = mode;
        }

        private void OnTransmitChanged(bool tx)
        {
            IsTransmitting = tx;
            if (!tx && MoxStateValue == MoxState.Transmitting) MoxStateValue = MoxState.Off;
        }

        private void OnSplitChanged(bool split)
        {
            if (_splitEcho.ShouldIgnore(split)) return;
            IsSplit = split;
        }

        private void OnSMeterChanged(int raw)
        {
            SMeterRaw = raw;
            SMeterSample?.Invoke(raw);
        }

        // ----- helpers ---------------------------------------------------------

        public void ReportError(string message) => StatusMessage = message;

        private AsyncRelayCommand Radio(Func<ICatInterface, Task> action)
            => new AsyncRelayCommand(() => action(_connection.Cat), ReportError, () => CanOperate);

        private void RefreshCommands() => CommandManager.InvalidateRequerySuggested();

        private static IntRange RangeFor(MultiParameter p)
        {
            switch (p)
            {
                case MultiParameter.AfGain: return FT891Ranges.AfGain;
                case MultiParameter.RfGain: return FT891Ranges.RfGain;
                case MultiParameter.MicGain: return FT891Ranges.MicGain;
                case MultiParameter.Squelch: return FT891Ranges.SquelchLevel;
                default: return FT891Ranges.TxPowerWatts;
            }
        }

        private static int StepFor(MultiParameter p)
        {
            switch (p)
            {
                case MultiParameter.AfGain:
                case MultiParameter.RfGain:
                    return 4;   // 0–255 ranges: coarse enough to feel responsive
                default:
                    return 1;
            }
        }

        private static Task<int> ReadMultiValueAsync(ICatInterface cat, MultiParameter p)
        {
            switch (p)
            {
                case MultiParameter.AfGain: return cat.GetAfGainAsync(CancellationToken.None);
                case MultiParameter.RfGain: return cat.GetRfGainAsync(CancellationToken.None);
                case MultiParameter.MicGain: return cat.GetMicGainAsync(CancellationToken.None);
                case MultiParameter.Squelch: return cat.GetSqlLevelAsync(CancellationToken.None);
                default: return cat.GetTxPowerAsync(CancellationToken.None);
            }
        }

        private static Task SetMultiValueAsync(ICatInterface cat, MultiParameter p, int value)
        {
            switch (p)
            {
                case MultiParameter.AfGain: return cat.SetAfGainAsync(value, CancellationToken.None);
                case MultiParameter.RfGain: return cat.SetRfGainAsync(value, CancellationToken.None);
                case MultiParameter.MicGain: return cat.SetMicGainAsync(value, CancellationToken.None);
                case MultiParameter.Squelch: return cat.SetSqlLevelAsync(value, CancellationToken.None);
                default: return cat.SetTxPowerAsync(value, CancellationToken.None);
            }
        }

        private static string FormatMulti(MultiParameter p, int value)
        {
            switch (p)
            {
                case MultiParameter.AfGain: return $"AF {value}";
                case MultiParameter.RfGain: return $"RF {value}";
                case MultiParameter.MicGain: return $"MIC {value}";
                case MultiParameter.Squelch: return $"SQL {value}";
                default: return $"PWR {value} W";
            }
        }
    }
}
