using System;
using System.Windows;

namespace YS891.RadioUI
{
    /// <summary>The front panel window.</summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>Last-resort error sink used by <see cref="App"/>.</summary>
        public void ReportUnhandledError(Exception ex)
        {
            Title = $"YS891 Radio UI — error: {ex.Message}";
        }
    }
}
