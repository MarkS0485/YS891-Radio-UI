using System.IO.Ports;
using System.Windows;
using YS891.RadioUI.Models;

namespace YS891.RadioUI.Views
{
    /// <summary>Connection picker: serial port, external TCP, or built-in simulator.</summary>
    public partial class ConnectDialog : Window
    {
        internal ConnectionSettings Result { get; private set; }

        internal ConnectDialog(ConnectionSettings last)
        {
            InitializeComponent();

            RefreshPorts(last.ComPort);
            HostBox.Text = last.Host;
            PortBox.Text = last.Port.ToString();

            switch (last.Kind)
            {
                case ConnectionKind.Serial: SerialOption.IsChecked = true; break;
                case ConnectionKind.Tcp: TcpOption.IsChecked = true; break;
                default: BuiltInOption.IsChecked = true; break;
            }
        }

        private void RefreshPorts(string preferred)
        {
            ComPortBox.Items.Clear();
            foreach (var name in SerialPort.GetPortNames())
                ComPortBox.Items.Add(name);
            if (!string.IsNullOrEmpty(preferred) && ComPortBox.Items.Contains(preferred))
                ComPortBox.SelectedItem = preferred;
            else if (ComPortBox.Items.Count > 0)
                ComPortBox.SelectedIndex = 0;
        }

        private void OnRefreshPorts(object sender, RoutedEventArgs e)
            => RefreshPorts(ComPortBox.SelectedItem as string);

        private void OnConnect(object sender, RoutedEventArgs e)
        {
            if (SerialOption.IsChecked == true)
            {
                if (ComPortBox.SelectedItem is not string port)
                {
                    ShowValidation("No COM port selected — is the radio's USB cable plugged in?");
                    return;
                }
                Result = new ConnectionSettings { Kind = ConnectionKind.Serial, ComPort = port };
            }
            else if (TcpOption.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(HostBox.Text) || !int.TryParse(PortBox.Text, out int tcpPort) || tcpPort < 1 || tcpPort > 65535)
                {
                    ShowValidation("Enter a host and a port between 1 and 65535.");
                    return;
                }
                Result = new ConnectionSettings { Kind = ConnectionKind.Tcp, Host = HostBox.Text.Trim(), Port = tcpPort };
            }
            else
            {
                Result = new ConnectionSettings { Kind = ConnectionKind.BuiltInSimulator };
            }

            DialogResult = true;
        }

        private void ShowValidation(string message)
        {
            ValidationText.Text = message;
            ValidationText.Visibility = Visibility.Visible;
        }
    }
}
