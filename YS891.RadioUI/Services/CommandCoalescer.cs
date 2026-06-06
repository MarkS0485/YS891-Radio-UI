using System;
using System.Threading.Tasks;
using FT891.Core;

namespace YS891.RadioUI.Services
{
    /// <summary>
    /// Latest-value-wins dispatcher for high-rate inputs (dial detents, knob turns).
    /// At most one write is in flight; intermediate values are discarded so the wire
    /// never lags behind the user's hand. Runs entirely on the UI thread — only the
    /// awaited CAT call yields.
    /// </summary>
    internal sealed class CommandCoalescer
    {
        private readonly Func<long, Task> _worker;
        private readonly Action<string> _reportError;
        private long? _pending;
        private bool _busy;

        public CommandCoalescer(Func<long, Task> worker, Action<string> reportError)
        {
            _worker = worker ?? throw new ArgumentNullException(nameof(worker));
            _reportError = reportError ?? throw new ArgumentNullException(nameof(reportError));
        }

        /// <summary>True while a write is in flight or queued — used by echo guards.</summary>
        public bool IsBusy => _busy || _pending.HasValue;

        /// <summary>The last value handed to the worker (or queued for it).</summary>
        public long? LastRequested { get; private set; }

        /// <summary>Request that <paramref name="value"/> be written; supersedes any queued value.</summary>
        public async void Request(long value)
        {
            LastRequested = value;
            _pending = value;
            if (_busy) return;

            _busy = true;
            try
            {
                while (_pending.HasValue)
                {
                    long next = _pending.Value;
                    _pending = null;
                    try
                    {
                        await _worker(next);
                    }
                    catch (FT891Exception ex)
                    {
                        _reportError(ex.Message);
                    }
                }
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Drop anything queued (e.g. on disconnect).</summary>
        public void Reset()
        {
            _pending = null;
            LastRequested = null;
        }
    }
}
