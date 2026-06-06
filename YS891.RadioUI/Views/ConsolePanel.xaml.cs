using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using FT891.Core;

namespace YS891.RadioUI.Views
{
    /// <summary>
    /// The escape hatch made visible: raw CAT commands, the live wire trace
    /// (FrameSent / FrameReceived), LastCommand / LastResponse / response bytes,
    /// and the engine tuning knobs (InterCommandDelayMs, TimeoutRetryCount).
    /// </summary>
    public partial class ConsolePanel : UserControl
    {
        private const int TraceLimit = 400;

        private ICatInterface _cat;
        private Action<string> _report = m => { };
        private Action<string> _onSent;
        private Action<string> _onReceived;
        private bool _loading;

        public ConsolePanel()
        {
            InitializeComponent();
            IsEnabled = false;

            DelaySlider.ValueChanged += (s, e) =>
            {
                DelayLabel.Text = $"Inter-command delay: {(int)e.NewValue} ms";
                if (!_loading && _cat != null) _cat.InterCommandDelayMs = (int)e.NewValue;
            };
            RetrySlider.ValueChanged += (s, e) =>
            {
                RetryLabel.Text = $"Timeout retries: {(int)e.NewValue}";
                if (!_loading && _cat != null) _cat.TimeoutRetryCount = (int)e.NewValue;
            };
        }

        internal void Attach(ICatInterface cat, Action<string> report)
        {
            _cat = cat;
            _report = report ?? (m => { });
            IsEnabled = true;

            _loading = true;
            DelaySlider.Value = cat.InterCommandDelayMs;
            DelayLabel.Text = $"Inter-command delay: {cat.InterCommandDelayMs} ms";
            RetrySlider.Value = cat.TimeoutRetryCount;
            RetryLabel.Text = $"Timeout retries: {cat.TimeoutRetryCount}";
            _loading = false;

            _onSent = frame => Trace($"→ {frame}");
            _onReceived = frame => Trace($"← {frame}");
            cat.FrameSent += _onSent;
            cat.FrameReceived += _onReceived;
        }

        internal void Detach()
        {
            if (_cat != null)
            {
                if (_onSent != null) _cat.FrameSent -= _onSent;
                if (_onReceived != null) _cat.FrameReceived -= _onReceived;
            }
            _cat = null;
            IsEnabled = false;
        }

        private void Trace(string line)
        {
            // Frame events can fire off the UI thread — marshal and cap the list.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (TraceToggle.IsChecked != true) return;
                TraceList.Items.Add(line);
                while (TraceList.Items.Count > TraceLimit) TraceList.Items.RemoveAt(0);
                if (TraceList.Items.Count > 0) TraceList.ScrollIntoView(TraceList.Items[TraceList.Items.Count - 1]);
            }));
        }

        private async void OnSendRaw(object s, RoutedEventArgs e)
        {
            var cat = _cat;
            if (cat == null) return;
            string command = RawBox.Text?.Trim();
            if (string.IsNullOrEmpty(command)) return;
            int expected = int.TryParse(ExpectedBox.Text, out int v) ? v : -1;
            try
            {
                string response = await cat.SendRawCommandAsync(command, expected, CancellationToken.None);
                RawResult.Text = $"sent     : {cat.LastCommand}\nresponse : {(string.IsNullOrEmpty(response) ? "(none)" : response)}";
            }
            catch (FT891Exception ex)
            {
                RawResult.Text = $"sent     : {command}\nerror    : {ex.Message}";
                _report(ex.Message);
            }
        }

        private void OnShowBytes(object s, RoutedEventArgs e)
        {
            var cat = _cat;
            if (cat == null) return;
            byte[] bytes = cat.LastResponseBytes();
            RawResult.Text = bytes == null || bytes.Length == 0
                ? "no response bytes recorded"
                : $"last response ({bytes.Length} bytes):\n{string.Join(" ", bytes.Select(b => b.ToString("X2")))}\nascii: {cat.LastResponse}";
        }

        private void OnClearTrace(object s, RoutedEventArgs e) => TraceList.Items.Clear();
    }
}
