using System;
using System.Threading.Tasks;
using System.Windows.Input;
using FT891.Core;

namespace YS891.RadioUI.ViewModels
{
    /// <summary>
    /// ICommand over an async handler. Re-entrancy is blocked while the handler runs,
    /// FT891Exception lands in the owner's status sink instead of the dispatcher,
    /// and cancellation is swallowed quietly.
    /// </summary>
    internal sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private readonly Action<string> _reportError;
        private bool _running;

        public AsyncRelayCommand(Func<Task> execute, Action<string> reportError, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _reportError = reportError ?? throw new ArgumentNullException(nameof(reportError));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Chained to CommandManager so buttons re-query whenever WPF suspects state
        /// changed (and whenever the VM calls InvalidateRequerySuggested).
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object parameter) => !_running && (_canExecute == null || _canExecute());

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;
            _running = true;
            RaiseCanExecuteChanged();
            try
            {
                await _execute();
            }
            catch (OperationCanceledException)
            {
                // Cancelled on purpose — nothing to report.
            }
            catch (FT891Exception ex)
            {
                _reportError(ex.Message);
            }
            finally
            {
                _running = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();
    }
}
