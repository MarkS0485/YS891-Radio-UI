using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YS891.RadioUI.ViewModels
{
    /// <summary>Minimal INotifyPropertyChanged base — no framework dependency.</summary>
    internal abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
