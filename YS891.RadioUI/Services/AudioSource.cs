using System;
using System.Collections.Generic;

namespace YS891.RadioUI.Services
{
    /// <summary>
    /// One front door for audio: device 0 is always "System audio (what you hear)"
    /// via WASAPI loopback — the right choice when the rig (or a YouTube video) is
    /// playing through the speakers — and the rest are real input devices via
    /// waveIn. Both paths deliver 16-bit mono at ~22.05 kHz.
    /// </summary>
    internal sealed class AudioSource : IDisposable
    {
        public const string LoopbackName = "System audio (what you hear)";

        private readonly AudioCaptureService _waveIn = new AudioCaptureService();
        private readonly WasapiLoopbackCapture _loopback = new WasapiLoopbackCapture();

        public AudioSource()
        {
            _waveIn.SamplesAvailable += s => SamplesAvailable?.Invoke(s);
            _loopback.SamplesAvailable += s => SamplesAvailable?.Invoke(s);
        }

        /// <summary>16-bit mono block at ~22.05 kHz (worker thread!).</summary>
        public event Action<short[]> SamplesAvailable;

        public bool IsRunning => _waveIn.IsRunning || _loopback.IsRunning;

        public static IReadOnlyList<string> GetDeviceNames()
        {
            var names = new List<string> { LoopbackName };
            names.AddRange(AudioCaptureService.GetDeviceNames());
            return names;
        }

        /// <summary>Index into <see cref="GetDeviceNames"/>: 0 = loopback, 1.. = waveIn.</summary>
        public void Start(int deviceIndex)
        {
            Stop();
            if (deviceIndex == 0) _loopback.Start();
            else _waveIn.Start(deviceIndex - 1);
        }

        public void Stop()
        {
            _waveIn.Stop();
            _loopback.Stop();
        }

        public void Dispose() => Stop();
    }
}
