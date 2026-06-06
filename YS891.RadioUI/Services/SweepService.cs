using System;
using System.Threading;
using System.Threading.Tasks;
using FT891.Core;

namespace YS891.RadioUI.Services
{
    /// <summary>One sampled point of a band sweep.</summary>
    internal sealed class SweepPoint
    {
        public SweepPoint(long hz, int raw, bool busy = false)
        {
            Hz = hz;
            Raw = raw;
            Busy = busy;
        }

        public long Hz { get; }

        /// <summary>Raw S-meter value, 0–255.</summary>
        public int Raw { get; }

        /// <summary>The radio's squelch-open/busy flag at this frequency (only read when asked).</summary>
        public bool Busy { get; }
    }

    /// <summary>
    /// One-shot band scope: steps the VFO across a span reading the S-meter at each
    /// stop. The FT-891 cannot stream spectrum over CAT, so this is the only honest
    /// way to draw one — it interrupts receive, which is why the caller pauses the
    /// monitor and shows a banner. The original frequency is restored on every exit
    /// path, including cancellation.
    /// </summary>
    internal static class SweepService
    {
        public static async Task RunAsync(
            ICatInterface cat,
            long lowHz,
            long highHz,
            long stepHz,
            IProgress<SweepPoint> progress,
            CancellationToken ct,
            bool includeBusy = false)
        {
            if (stepHz <= 0) throw new ArgumentOutOfRangeException(nameof(stepHz));

            long restore = await cat.GetVfoAFrequencyAsync(ct);
            try
            {
                for (long hz = lowHz; hz <= highHz; hz += stepHz)
                {
                    ct.ThrowIfCancellationRequested();
                    await cat.SetVfoAFrequencyAsync(hz, ct);
                    int raw = await cat.GetSMeterAsync(ct);
                    bool busy = includeBusy && await cat.GetBusyAsync(ct);
                    progress.Report(new SweepPoint(hz, raw, busy));
                }
            }
            finally
            {
                // Never strand the operator off-frequency — restore even when cancelled.
                await cat.SetVfoAFrequencyAsync(restore, CancellationToken.None);
            }
        }
    }
}
