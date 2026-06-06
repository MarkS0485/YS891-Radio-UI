using System;

namespace YS891.RadioUI.Services
{
    /// <summary>Radix-2 FFT with a Hann window — enough DSP for a front panel.</summary>
    internal static class Fft
    {
        /// <summary>
        /// Magnitudes (linear, windowed) of the first N/2 bins of a power-of-two
        /// sample block. Input is 16-bit audio; output length is samples.Length/2.
        /// </summary>
        public static double[] Magnitudes(short[] samples)
        {
            int n = samples.Length;
            if ((n & (n - 1)) != 0) throw new ArgumentException("Sample count must be a power of two.", nameof(samples));

            var re = new double[n];
            var im = new double[n];
            for (int i = 0; i < n; i++)
            {
                double hann = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
                re[i] = samples[i] / 32768.0 * hann;
            }

            Transform(re, im);

            var mags = new double[n / 2];
            for (int i = 0; i < n / 2; i++)
                mags[i] = Math.Sqrt(re[i] * re[i] + im[i] * im[i]) / n * 4; // window + scale compensation
            return mags;
        }

        private static void Transform(double[] re, double[] im)
        {
            int n = re.Length;

            // Bit-reversal permutation.
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2 * Math.PI / len;
                double wRe = Math.Cos(angle), wIm = Math.Sin(angle);
                for (int i = 0; i < n; i += len)
                {
                    double curRe = 1, curIm = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = i + k + len / 2;
                        double tRe = re[b] * curRe - im[b] * curIm;
                        double tIm = re[b] * curIm + im[b] * curRe;
                        re[b] = re[a] - tRe;
                        im[b] = im[a] - tIm;
                        re[a] += tRe;
                        im[a] += tIm;
                        double nextRe = curRe * wRe - curIm * wIm;
                        curIm = curRe * wIm + curIm * wRe;
                        curRe = nextRe;
                    }
                }
            }
        }
    }
}
