using System;
using System.Collections.Generic;

namespace YS891.RadioUI.Services
{
    /// <summary>
    /// A toy "ether" for the built-in simulator, ported from FT891.Demo: a handful
    /// of made-up signals scattered across the 20 m band, each a Gaussian bump on a
    /// noise floor, some pulsing on and off like real traffic. Plugged into the
    /// simulator as its SignalSource it makes the virtual radio's S-meter respond
    /// to tuning — so the sweep, chatter scan and heatmap actually find something.
    /// </summary>
    internal sealed class BandModel
    {
        internal readonly struct Signal
        {
            public readonly long CenterHz;
            public readonly int Peak;        // S-meter contribution at the centre (0..255)
            public readonly double WidthHz;  // Gaussian sigma
            public readonly string Label;
            public readonly double Duty;     // fraction of the time it's on air
            public Signal(long c, int p, double w, string label, double duty)
            { CenterHz = c; Peak = p; WidthHz = w; Label = label; Duty = duty; }
        }

        private readonly Signal[] _signals;
        public int NoiseFloor { get; }
        public long LowHz { get; }
        public long HighHz { get; }

        public BandModel()
        {
            NoiseFloor = 10;
            LowHz = 14_000_000;
            HighHz = 14_350_000;
            _signals = new[]
            {
                new Signal(14_070_000, 205,  900, "FT8",      0.95),
                new Signal(14_100_000, 110,  400, "NCDXF BCN",1.00),
                new Signal(14_195_000, 235, 1400, "DX SSB",   0.70),
                new Signal(14_230_000, 150, 1100, "SSTV",     0.45),
                new Signal(14_250_000, 185, 1400, "RAG-CHEW", 0.80),
                new Signal(14_313_000, 140, 1200, "MARITIME", 0.55),
            };
        }

        public IReadOnlyList<Signal> Signals => _signals;

        /// <summary>S-meter reading (0..255) at <paramref name="hz"/> for animation frame <paramref name="tick"/>.</summary>
        public int StrengthAt(long hz, int tick = 0)
        {
            double v = NoiseFloor + Hash(hz / 200, tick) * 16.0; // banded noise, ~200 Hz granularity
            foreach (Signal s in _signals)
            {
                if (Hash(s.CenterHz, tick >> 3) > s.Duty) continue; // pulsing on/off over ~8 frames
                double d = hz - s.CenterHz;
                v += s.Peak * Math.Exp(-(d * d) / (2.0 * s.WidthHz * s.WidthHz));
            }
            return (int)Math.Max(0, Math.Min(255, v));
        }

        /// <summary>Deterministic 0..1 pseudo-noise from a frequency band and a tick — stable per (a,b), animates with tick.</summary>
        private static double Hash(long a, int b)
        {
            unchecked
            {
                ulong h = (ulong)(a * 2654435761L) ^ ((ulong)b * 40503UL);
                h ^= h >> 13; h *= 1274126177UL; h ^= h >> 16;
                return (h & 0xFFFF) / 65535.0;
            }
        }
    }
}
