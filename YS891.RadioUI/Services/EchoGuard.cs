using System.Collections.Generic;

namespace YS891.RadioUI.Services
{
    /// <summary>
    /// Stops monitor echoes from fighting in-flight user input. While a write is
    /// pending, every report is ignored. Just after the last write completes, a
    /// poll that read the radio *before* our write landed may still arrive — so
    /// for a couple of events we distrust reports that differ from what we wrote,
    /// and accept as soon as the radio confirms our value (or the window expires,
    /// which lets genuine radio-side changes through).
    /// </summary>
    internal sealed class EchoGuard<T>
    {
        private const int SettleEventCount = 2;

        private int _pendingWrites;
        private int _settleEvents;
        private T _lastWritten;
        private bool _hasWritten;

        public void BeginWrite(T value)
        {
            _pendingWrites++;
            _lastWritten = value;
            _hasWritten = true;
        }

        public void EndWrite()
        {
            if (_pendingWrites > 0) _pendingWrites--;
            if (_pendingWrites == 0) _settleEvents = SettleEventCount;
        }

        public bool ShouldIgnore(T reported)
        {
            if (_pendingWrites > 0) return true;
            if (_settleEvents > 0)
            {
                if (_hasWritten && EqualityComparer<T>.Default.Equals(reported, _lastWritten))
                {
                    _settleEvents = 0;
                    return false; // radio confirmed our value — back to normal
                }
                _settleEvents--;
                return true; // stale pre-write poll
            }
            return false;
        }

        public void Reset()
        {
            _pendingWrites = 0;
            _settleEvents = 0;
            _hasWritten = false;
        }
    }
}
