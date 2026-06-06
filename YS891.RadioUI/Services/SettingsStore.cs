using System;
using System.Configuration;
using YS891.RadioUI.Models;

namespace YS891.RadioUI.Services
{
    /// <summary>Persists the last successful connection choice between runs.</summary>
    internal sealed class SettingsStore : ApplicationSettingsBase
    {
        [UserScopedSetting, DefaultSettingValue("BuiltInSimulator")]
        public string LastKind
        {
            get => (string)this[nameof(LastKind)];
            set => this[nameof(LastKind)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("COM8")]
        public string LastComPort
        {
            get => (string)this[nameof(LastComPort)];
            set => this[nameof(LastComPort)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("127.0.0.1")]
        public string LastHost
        {
            get => (string)this[nameof(LastHost)];
            set => this[nameof(LastHost)] = value;
        }

        [UserScopedSetting, DefaultSettingValue("4000")]
        public int LastPort
        {
            get => (int)this[nameof(LastPort)];
            set => this[nameof(LastPort)] = value;
        }

        public ConnectionSettings LoadConnection()
        {
            var kind = Enum.TryParse(LastKind, out ConnectionKind parsed)
                ? parsed
                : ConnectionKind.BuiltInSimulator;
            return new ConnectionSettings
            {
                Kind = kind,
                ComPort = LastComPort,
                Host = string.IsNullOrWhiteSpace(LastHost) ? "127.0.0.1" : LastHost,
                Port = LastPort > 0 ? LastPort : 4000,
            };
        }

        public void SaveConnection(ConnectionSettings settings)
        {
            LastKind = settings.Kind.ToString();
            LastComPort = settings.ComPort ?? "";
            LastHost = settings.Host ?? "127.0.0.1";
            LastPort = settings.Port;
            Save();
        }
    }
}
