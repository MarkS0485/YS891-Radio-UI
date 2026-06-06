namespace YS891.RadioUI.Models
{
    /// <summary>Dial step sizes, cycled by the FAST/STEP key.</summary>
    internal static class TuningStep
    {
        public static readonly long[] StepsHz = { 10, 100, 1_000, 10_000 };

        public static string Label(long stepHz)
            => stepHz >= 1_000 ? $"{stepHz / 1_000} kHz" : $"{stepHz} Hz";

        public static long Next(long currentHz)
        {
            for (int i = 0; i < StepsHz.Length; i++)
                if (StepsHz[i] == currentHz)
                    return StepsHz[(i + 1) % StepsHz.Length];
            return StepsHz[0];
        }
    }
}
