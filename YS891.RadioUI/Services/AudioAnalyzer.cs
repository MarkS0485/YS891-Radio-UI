using System;

namespace YS891.RadioUI.Services
{
    /// <summary>
    /// Splits the capture stream into the things the views want: raw blocks for
    /// the waveform, windowed FFT frames for the spectrum/waterfall, and an RMS
    /// level for anything that wants to pulse to the music. Call Push on the UI
    /// thread; events fire synchronously on it.
    /// </summary>
    internal sealed class AudioAnalyzer
    {
        private const int FftSize = 2_048;

        private readonly short[] _window = new short[FftSize];
        private int _fill;

        /// <summary>Raw block, as captured (for the waveform view).</summary>
        public event Action<short[]> WaveformBlock;

        /// <summary>Linear FFT magnitudes, FftSize/2 bins at ~10.8 Hz/bin.</summary>
        public event Action<double[]> SpectrumFrame;

        /// <summary>RMS level 0–1 of the latest block.</summary>
        public event Action<double> Level;

        public void Push(short[] samples)
        {
            WaveformBlock?.Invoke(samples);

            if (Level != null)
            {
                double sum = 0;
                foreach (short s in samples) sum += (double)s * s;
                Level(Math.Sqrt(sum / samples.Length) / 32768.0);
            }

            // Accumulate into the FFT window; emit a frame each time it fills.
            int offset = 0;
            while (offset < samples.Length)
            {
                int n = Math.Min(FftSize - _fill, samples.Length - offset);
                Array.Copy(samples, offset, _window, _fill, n);
                _fill += n;
                offset += n;
                if (_fill == FftSize)
                {
                    _fill = 0;
                    SpectrumFrame?.Invoke(Fft.Magnitudes(_window));
                }
            }
        }
    }
}
