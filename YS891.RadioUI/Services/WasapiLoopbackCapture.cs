using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace YS891.RadioUI.Services
{
    /// <summary>
    /// "What you hear": captures the default output device via WASAPI loopback,
    /// so the views can analyse whatever Windows is playing (the rig's USB audio,
    /// a YouTube video, anything). Hand-rolled COM interop — still no packages.
    /// Output is normalised to 16-bit mono at ~22.05 kHz to match the mic path.
    /// </summary>
    internal sealed class WasapiLoopbackCapture : IDisposable
    {
        private const int TargetRate = 22_050;
        private const int BlockSamples = 1_024;

        private IAudioClient _client;
        private IAudioCaptureClient _capture;
        private Thread _pump;
        private volatile bool _running;

        private int _sourceRate;
        private int _sourceChannels;
        private bool _sourceIsFloat;
        private int _sourceBits;

        /// <summary>One 16-bit mono block at ~22.05 kHz (worker thread!).</summary>
        public event Action<short[]> SamplesAvailable;

        public bool IsRunning => _running;

        public void Start()
        {
            if (_running) return;

            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            int hr = enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out IMMDevice device);
            if (hr != 0) throw new InvalidOperationException($"No default output device (0x{hr:X8}).");

            var iidAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
            device.Activate(ref iidAudioClient, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out object clientObj);
            _client = (IAudioClient)clientObj;

            _client.GetMixFormat(out IntPtr formatPtr);
            ParseFormat(formatPtr);

            const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
            hr = _client.Initialize(0 /* shared */, AUDCLNT_STREAMFLAGS_LOOPBACK, 2_000_000 /* 200 ms */, 0, formatPtr, IntPtr.Zero);
            Marshal.FreeCoTaskMem(formatPtr);
            if (hr != 0) throw new InvalidOperationException($"Loopback init failed (0x{hr:X8}).");

            var iidCapture = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
            _client.GetService(ref iidCapture, out object captureObj);
            _capture = (IAudioCaptureClient)captureObj;

            _running = true;
            _pump = new Thread(PumpLoop) { IsBackground = true, Name = "WasapiLoopbackPump" };
            _pump.Start();
            _client.Start();
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            _pump?.Join(1000);
            _pump = null;
            try { _client?.Stop(); } catch (COMException) { /* device went away */ }
            if (_capture != null) { Marshal.ReleaseComObject(_capture); _capture = null; }
            if (_client != null) { Marshal.ReleaseComObject(_client); _client = null; }
        }

        public void Dispose() => Stop();

        private void ParseFormat(IntPtr formatPtr)
        {
            // WAVEFORMATEX header: tag(2) channels(2) rate(4) avg(4) align(2) bits(2) cb(2)
            int tag = Marshal.ReadInt16(formatPtr, 0) & 0xFFFF;
            _sourceChannels = Marshal.ReadInt16(formatPtr, 2);
            _sourceRate = Marshal.ReadInt32(formatPtr, 4);
            _sourceBits = Marshal.ReadInt16(formatPtr, 14);
            if (tag == 0xFFFE) // WAVE_FORMAT_EXTENSIBLE: SubFormat GUID starts at offset 24
                _sourceIsFloat = Marshal.ReadInt32(formatPtr, 24) == 3;
            else
                _sourceIsFloat = tag == 3; // WAVE_FORMAT_IEEE_FLOAT
        }

        private void PumpLoop()
        {
            var block = new short[BlockSamples];
            int fill = 0;
            double step = (double)_sourceRate / TargetRate;
            double cursor = 0;

            while (_running)
            {
                int hr = _capture.GetNextPacketSize(out uint packetFrames);
                if (hr != 0) break;

                if (packetFrames == 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

                _capture.GetBuffer(out IntPtr data, out uint frames, out uint flags, IntPtr.Zero, IntPtr.Zero);
                bool silent = (flags & 2 /* AUDCLNT_BUFFERFLAGS_SILENT */) != 0;

                // Mono-mix then decimate to the target rate with a fractional cursor.
                for (; cursor < frames; cursor += step)
                {
                    block[fill++] = silent ? (short)0 : ReadFrameMono(data, (int)cursor);
                    if (fill == BlockSamples)
                    {
                        var copy = new short[BlockSamples];
                        Buffer.BlockCopy(block, 0, copy, 0, BlockSamples * 2);
                        SamplesAvailable?.Invoke(copy);
                        fill = 0;
                    }
                }
                cursor -= frames;

                _capture.ReleaseBuffer(frames);
            }
        }

        private readonly byte[] _floatScratch = new byte[4];

        private short ReadFrameMono(IntPtr data, int frame)
        {
            int bytesPerSample = _sourceBits / 8;
            int offset = frame * _sourceChannels * bytesPerSample;
            double sum = 0;
            for (int ch = 0; ch < _sourceChannels; ch++)
            {
                int o = offset + ch * bytesPerSample;
                if (_sourceIsFloat)
                {
                    Marshal.Copy(IntPtr.Add(data, o), _floatScratch, 0, 4);
                    sum += BitConverter.ToSingle(_floatScratch, 0);
                }
                else if (_sourceBits == 16)
                {
                    sum += Marshal.ReadInt16(data, o) / 32768.0;
                }
                else // 32-bit PCM
                {
                    sum += Marshal.ReadInt32(data, o) / 2147483648.0;
                }
            }
            double mono = sum / _sourceChannels;
            return (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, mono * 32767));
        }

        // ----- COM interop -------------------------------------------------------

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr audioSessionGuid);
            int GetBufferSize(out uint bufferFrames);
            int GetStreamLatency(out long latency);
            int GetCurrentPadding(out uint padding);
            int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
            int GetMixFormat(out IntPtr format);
            int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
            int Start();
            int Stop();
            int Reset();
            int SetEventHandle(IntPtr eventHandle);
            int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }

        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            int GetBuffer(out IntPtr data, out uint frames, out uint flags, IntPtr devicePosition, IntPtr qpcPosition);
            int ReleaseBuffer(uint frames);
            int GetNextPacketSize(out uint frames);
        }
    }
}
