using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace YS891.RadioUI.Services
{
    /// <summary>
    /// Microphone/line-in capture over winmm waveIn — no extra dependencies.
    /// CAT cannot carry audio: the FT-891's RX audio shows up in Windows as a USB
    /// audio device, so we capture whichever input the user picks (any mic works
    /// for testing). 22.05 kHz, 16-bit mono; <see cref="SamplesAvailable"/> fires
    /// on a worker thread — marshal to the dispatcher before touching UI.
    /// </summary>
    internal sealed class AudioCaptureService : IDisposable
    {
        private const int SampleRate = 22_050;
        private const int SamplesPerBuffer = 1_024;
        private const int BufferCount = 4;

        private readonly object _gate = new object();
        private readonly AutoResetEvent _dataReady = new AutoResetEvent(false);

        private IntPtr _handle;
        private GCHandle[] _headerPins;
        private GCHandle[] _dataPins;
        private short[][] _buffers;
        private Thread _pump;
        private volatile bool _running;

        /// <summary>One captured block of 16-bit mono samples (worker thread!).</summary>
        public event Action<short[]> SamplesAvailable;

        public bool IsRunning => _running;

        public static IReadOnlyList<string> GetDeviceNames()
        {
            int count = waveInGetNumDevs();
            var names = new List<string>(count);
            for (uint i = 0; i < count; i++)
            {
                var caps = new WAVEINCAPS();
                if (waveInGetDevCaps(new IntPtr(i), ref caps, Marshal.SizeOf<WAVEINCAPS>()) == 0)
                    names.Add(caps.szPname);
            }
            return names;
        }

        public void Start(int deviceIndex)
        {
            lock (_gate)
            {
                if (_running) Stop();

                var format = new WAVEFORMATEX
                {
                    wFormatTag = 1, // PCM
                    nChannels = 1,
                    nSamplesPerSec = SampleRate,
                    wBitsPerSample = 16,
                    nBlockAlign = 2,
                    nAvgBytesPerSec = SampleRate * 2,
                    cbSize = 0,
                };

                int rc = waveInOpen(out _handle, deviceIndex, ref format, _dataReady.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
                if (rc != 0) throw new InvalidOperationException($"waveInOpen failed (code {rc}). Is the audio device available?");

                _buffers = new short[BufferCount][];
                _headerPins = new GCHandle[BufferCount];
                _dataPins = new GCHandle[BufferCount];
                for (int i = 0; i < BufferCount; i++)
                {
                    _buffers[i] = new short[SamplesPerBuffer];
                    _dataPins[i] = GCHandle.Alloc(_buffers[i], GCHandleType.Pinned);
                    var header = new WAVEHDR
                    {
                        lpData = _dataPins[i].AddrOfPinnedObject(),
                        dwBufferLength = SamplesPerBuffer * 2,
                    };
                    _headerPins[i] = GCHandle.Alloc(header, GCHandleType.Pinned);
                    waveInPrepareHeader(_handle, _headerPins[i].AddrOfPinnedObject(), Marshal.SizeOf<WAVEHDR>());
                    waveInAddBuffer(_handle, _headerPins[i].AddrOfPinnedObject(), Marshal.SizeOf<WAVEHDR>());
                }

                _running = true;
                _pump = new Thread(PumpLoop) { IsBackground = true, Name = "AudioCapturePump" };
                _pump.Start();
                waveInStart(_handle);
            }
        }

        public void Stop()
        {
            Thread pump;
            lock (_gate)
            {
                if (!_running) return;
                _running = false;
                pump = _pump;
                _pump = null;
            }

            _dataReady.Set(); // wake the pump so it can exit
            pump?.Join(1000);

            lock (_gate)
            {
                if (_handle == IntPtr.Zero) return;
                waveInStop(_handle);
                waveInReset(_handle);
                for (int i = 0; i < BufferCount; i++)
                {
                    waveInUnprepareHeader(_handle, _headerPins[i].AddrOfPinnedObject(), Marshal.SizeOf<WAVEHDR>());
                    _headerPins[i].Free();
                    _dataPins[i].Free();
                }
                waveInClose(_handle);
                _handle = IntPtr.Zero;
            }
        }

        public void Dispose() => Stop();

        private void PumpLoop()
        {
            while (_running)
            {
                _dataReady.WaitOne(250);
                if (!_running) break;

                for (int i = 0; i < BufferCount; i++)
                {
                    var header = Marshal.PtrToStructure<WAVEHDR>(_headerPins[i].AddrOfPinnedObject());
                    if ((header.dwFlags & WHDR_DONE) == 0) continue;

                    var copy = new short[SamplesPerBuffer];
                    Buffer.BlockCopy(_buffers[i], 0, copy, 0, SamplesPerBuffer * 2);
                    SamplesAvailable?.Invoke(copy);

                    header.dwFlags &= ~WHDR_DONE;
                    Marshal.StructureToPtr(header, _headerPins[i].AddrOfPinnedObject(), false);
                    waveInAddBuffer(_handle, _headerPins[i].AddrOfPinnedObject(), Marshal.SizeOf<WAVEHDR>());
                }
            }
        }

        // ----- winmm interop ---------------------------------------------------

        private const int CALLBACK_EVENT = 0x00050000;
        private const int WHDR_DONE = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public int dwBufferLength;
            public int dwBytesRecorded;
            public IntPtr dwUser;
            public int dwFlags;
            public int dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WAVEINCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwFormats;
            public ushort wChannels;
            public ushort wReserved1;
        }

        [DllImport("winmm.dll")] private static extern int waveInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Auto)] private static extern int waveInGetDevCaps(IntPtr deviceId, ref WAVEINCAPS caps, int size);
        [DllImport("winmm.dll")] private static extern int waveInOpen(out IntPtr handle, int deviceId, ref WAVEFORMATEX format, IntPtr callback, IntPtr instance, int flags);
        [DllImport("winmm.dll")] private static extern int waveInPrepareHeader(IntPtr handle, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInUnprepareHeader(IntPtr handle, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInAddBuffer(IntPtr handle, IntPtr header, int size);
        [DllImport("winmm.dll")] private static extern int waveInStart(IntPtr handle);
        [DllImport("winmm.dll")] private static extern int waveInStop(IntPtr handle);
        [DllImport("winmm.dll")] private static extern int waveInReset(IntPtr handle);
        [DllImport("winmm.dll")] private static extern int waveInClose(IntPtr handle);
    }
}
