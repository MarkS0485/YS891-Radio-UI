namespace YS891.RadioUI.Models
{
    /// <summary>How the panel reaches a radio.</summary>
    internal enum ConnectionKind
    {
        /// <summary>Real FT-891 on a serial COM port.</summary>
        Serial,

        /// <summary>External FT891.Simulator (or remote bridge) over TCP.</summary>
        Tcp,

        /// <summary>SimulatorServer hosted in-process on an ephemeral port.</summary>
        BuiltInSimulator,
    }

    /// <summary>One picked connection target.</summary>
    internal sealed class ConnectionSettings
    {
        public ConnectionKind Kind { get; set; } = ConnectionKind.BuiltInSimulator;
        public string ComPort { get; set; } = "";
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 4000;

        public override string ToString()
        {
            switch (Kind)
            {
                case ConnectionKind.Serial: return $"Serial {ComPort}";
                case ConnectionKind.Tcp: return $"TCP {Host}:{Port}";
                default: return "Built-in simulator";
            }
        }
    }
}
