using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FT891.Core;

namespace YS891.RadioUI.Views
{
    /// <summary>Memory channel operations — the full set the library exposes.</summary>
    public partial class MemoryPanel : UserControl
    {
        private sealed class ChannelRow
        {
            public int Channel { get; set; }
            public string Frequency { get; set; }
            public string Mode { get; set; }
        }

        private readonly ObservableCollection<ChannelRow> _rows = new ObservableCollection<ChannelRow>();
        private ICatInterface _cat;
        private Action<string> _report = m => { };

        public MemoryPanel()
        {
            InitializeComponent();
            ChannelList.ItemsSource = _rows;
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

        private int Channel => int.TryParse(ChannelBox.Text, out int ch) ? Math.Max(1, Math.Min(99, ch)) : 1;

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

        private async void OnChUp(object s, RoutedEventArgs e)
            => await Apply("Channel up", async c =>
            {
                await c.ChannelUpDownAsync(true, CancellationToken.None);
                ChannelBox.Text = (await c.GetMemoryChannelAsync(CancellationToken.None)).ToString();
            });

        private async void OnChDown(object s, RoutedEventArgs e)
            => await Apply("Channel down", async c =>
            {
                await c.ChannelUpDownAsync(false, CancellationToken.None);
                ChannelBox.Text = (await c.GetMemoryChannelAsync(CancellationToken.None)).ToString();
            });

        private async void OnSetChannel(object s, RoutedEventArgs e)
            => await Apply($"Go to channel {Channel}", c => c.SetMemoryChannelAsync(Channel, CancellationToken.None));

        private async void OnReadChannel(object s, RoutedEventArgs e)
            => await Apply($"Read channel {Channel}", async c =>
            {
                var info = await c.ReadMemoryAsync(Channel, CancellationToken.None);
                ChannelInfo.Text = $"ch {Channel}: {FrequencyFormat.ToFormattedString(info.FrequencyHz)}  {info.Mode}";
            });

        private async void OnWriteMemory(object s, RoutedEventArgs e)
            => await Apply($"Write VFO to channel {Channel}", async c =>
            {
                long hz = await c.GetVfoAFrequencyAsync(CancellationToken.None);
                var mode = await c.GetModeAsync(CancellationToken.None);
                await c.WriteMemoryAsync(Channel, hz, mode, TagBox.Text ?? "", CancellationToken.None);
                ChannelInfo.Text = $"ch {Channel}: ← {FrequencyFormat.ToFormattedString(hz)} {mode}";
            });

        private async void OnVfoToMem(object s, RoutedEventArgs e)
            => await Apply("VFO A → memory", c => c.CopyVfoAToMemoryAsync(CancellationToken.None));

        private async void OnMemToVfo(object s, RoutedEventArgs e)
            => await Apply("Memory → VFO A", c => c.CopyMemoryToVfoAAsync(CancellationToken.None));

        private async void OnQmbStore(object s, RoutedEventArgs e)
            => await Apply("QMB store", c => c.StoreToQuickMemoryBankAsync(CancellationToken.None));

        private async void OnQmbRecall(object s, RoutedEventArgs e)
            => await Apply("QMB recall", c => c.RecallQuickMemoryBankAsync(CancellationToken.None));

        private async void OnReadAll(object s, RoutedEventArgs e)
        {
            var cat = _cat;
            if (cat == null) return;
            ReadAllButton.IsEnabled = false;
            _rows.Clear();
            try
            {
                for (int ch = 1; ch <= 99 && _cat != null; ch++)
                {
                    ReadAllProgress.Text = $"reading {ch}/99…";
                    try
                    {
                        var info = await cat.ReadMemoryAsync(ch, CancellationToken.None);
                        if (info.FrequencyHz > 0)
                            _rows.Add(new ChannelRow
                            {
                                Channel = ch,
                                Frequency = FrequencyFormat.ToFormattedString(info.FrequencyHz),
                                Mode = info.Mode.ToString(),
                            });
                    }
                    catch (FT891Exception)
                    {
                        // Empty/invalid channel — skip it.
                    }
                }
                ReadAllProgress.Text = $"{_rows.Count} programmed channels.";
            }
            finally
            {
                ReadAllButton.IsEnabled = true;
            }
        }

        private async void OnChannelActivate(object s, RoutedEventArgs e)
        {
            if (ChannelList.SelectedItem is not ChannelRow row) return;
            ChannelBox.Text = row.Channel.ToString();
            await Apply($"Recall channel {row.Channel}", async c =>
            {
                await c.SetMemoryChannelAsync(row.Channel, CancellationToken.None);
                await c.CopyMemoryToVfoAAsync(CancellationToken.None);
            });
        }
    }
}
