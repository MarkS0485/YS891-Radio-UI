using System.Windows;
using System.Windows.Threading;

namespace YS891.RadioUI
{
    /// <summary>
    /// Application entry point. CAT failures must never crash the panel, so the
    /// dispatcher net catches anything a command handler failed to.
    /// </summary>
    public partial class App : Application
    {
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (MainWindow is MainWindow window)
            {
                window.ReportUnhandledError(e.Exception);
                e.Handled = true;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            base.OnStartup(e);
        }
    }
}
