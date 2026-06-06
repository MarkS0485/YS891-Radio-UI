using System;
using FT891.Core;
using FT891.Simulator;
using FT891.Simulator.Morse;
using YS891.RadioUI.Models;

namespace YS891.RadioUI.Services
{
    /// <summary>
    /// A live link to a radio: the CAT client plus anything the link owns
    /// (the in-process simulator, when that option is picked).
    /// </summary>
    internal sealed class RadioConnection : IDisposable
    {
        private readonly IDisposable _ownedServer;

        public RadioConnection(ICatInterface cat, string description, IDisposable ownedServer = null)
        {
            Cat = cat ?? throw new ArgumentNullException(nameof(cat));
            Description = description;
            _ownedServer = ownedServer;
        }

        public ICatInterface Cat { get; }
        public string Description { get; }

        public void Dispose()
        {
            try { Cat.Disconnect(); } catch (FT891Exception) { /* already gone */ }
            Cat.Dispose();
            _ownedServer?.Dispose();
        }
    }

    /// <summary>Builds a <see cref="RadioConnection"/> from picked settings.</summary>
    internal static class RadioConnectionFactory
    {
        public static RadioConnection Create(ConnectionSettings settings)
        {
            switch (settings.Kind)
            {
                case ConnectionKind.Serial:
                    return new RadioConnection(
                        new FT891Cat(settings.ComPort),
                        $"Serial {settings.ComPort}");

                case ConnectionKind.Tcp:
                    return new RadioConnection(
                        new FT891Cat(new TcpCatTransport(settings.Host, settings.Port)),
                        $"TCP {settings.Host}:{settings.Port}");

                case ConnectionKind.BuiltInSimulator:
                    // A silent SimulatorServer answers every command but its S-meter
                    // is stuck at zero — so give it an ether: the ported Demo band
                    // model (six 20 m stations) plus a decodable CW beacon, exactly
                    // how FT891.Demo wires its simulator.
                    var band = new BandModel();
                    var beacon = new CwBeacon(14_058_000, "VVV VVV DE YS891 YS891 = CQ CQ CQ DE YS891 K", 16);
                    var state = new RadioState { VfoAHz = 14_250_000, Mode = OperatingMode.USB };
                    var server = new SimulatorServer(port: 0, state: state)
                    {
                        SignalSource = hz => Math.Max(
                            band.StrengthAt(hz, Environment.TickCount / 120),
                            beacon.SignalAt(hz, Environment.TickCount)),
                        BusyThreshold = 55,
                    };
                    server.Start();
                    return new RadioConnection(
                        new FT891Cat(new TcpCatTransport("127.0.0.1", server.Port)),
                        $"Built-in simulator (port {server.Port}, 20 m band activity + CW beacon)",
                        server);

                default:
                    throw new ArgumentOutOfRangeException(nameof(settings));
            }
        }
    }
}
